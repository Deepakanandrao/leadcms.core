// <copyright file="EnrichmentExecutorTask.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Diagnostics;
using LeadCMS.Data;
using LeadCMS.Enrichment.Interfaces;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Services;
using LeadCMS.Tasks;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Enrichment.Tasks;

/// <summary>
/// Executes pending enrichment work items by calling providers, enforcing quotas, logging attempts/audit.
/// </summary>
public class EnrichmentExecutorTask(
    IConfiguration configuration,
    PgDbContext dbContext,
    TaskStatusService taskStatusService,
    IEnrichmentProviderResolver providerResolver,
    IEnrichmentWorkItemService workItemService,
    IEnrichmentQuotaService quotaService,
    IEnrichmentAuditService auditService) : BaseTask("Tasks:EnrichmentExecutorTask", configuration, taskStatusService)
{
    private readonly PgDbContext dbContext = dbContext;
    private readonly IEnrichmentProviderResolver providerResolver = providerResolver;
    private readonly IEnrichmentWorkItemService workItemService = workItemService;
    private readonly IEnrichmentQuotaService quotaService = quotaService;
    private readonly IEnrichmentAuditService auditService = auditService;

    public override async Task<bool> Execute(TaskExecutionLog currentJob)
    {
        var batchSize = 50;
        var workItems = await workItemService.GetPendingAsync(batchSize);

        if (workItems.Count == 0)
        {
            return true;
        }

        var providerConfigs = await dbContext.EnrichmentProviderConfigs!
            .Where(c => c.Enabled)
            .ToDictionaryAsync(c => c.ProviderKey, StringComparer.OrdinalIgnoreCase);

        var providerLastAttempts = await dbContext.EnrichmentProviderAttempts!
            .Where(a => workItems.Select(w => w.ProviderKey).Distinct().Contains(a.ProviderKey))
            .GroupBy(a => a.ProviderKey)
            .Select(g => new { ProviderKey = g.Key, LastAttempt = g.Max(a => a.CreatedAt) })
            .ToDictionaryAsync(x => x.ProviderKey, x => x.LastAttempt, StringComparer.OrdinalIgnoreCase);

        var inProgressCounts = await dbContext.EnrichmentWorkItems!
            .Where(w => w.Status == EnrichmentWorkItemStatus.InProgress)
            .GroupBy(w => w.ProviderKey)
            .Select(g => new { ProviderKey = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProviderKey, x => x.Count, StringComparer.OrdinalIgnoreCase);

        var executionTasks = new List<Task>();
        var currentConcurrency = new Dictionary<string, int>(inProgressCounts, StringComparer.OrdinalIgnoreCase);

        foreach (var workItem in workItems)
        {
            if (!providerConfigs.TryGetValue(workItem.ProviderKey, out var providerConfig))
            {
                continue;
            }

            if (!CanExecuteNow(providerConfig, providerLastAttempts, currentConcurrency))
            {
                continue;
            }

            if (providerConfig.AllowParallelCalls == false)
            {
                await Task.WhenAll(executionTasks);
                executionTasks.Clear();

                try
                {
                    await ExecuteWorkItemAsync(workItem, providerConfig);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Unexpected error executing work item {workItem.Id} for provider {workItem.ProviderKey}");
                    workItemService.MarkFailed(workItem, incrementRetry: true);
                    await dbContext.SaveChangesAsync();
                }
            }
            else
            {
                var task = Task.Run(async () =>
                {
                    try
                    {
                        await ExecuteWorkItemAsync(workItem, providerConfig);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Unexpected error executing work item {workItem.Id} for provider {workItem.ProviderKey}");
                        workItemService.MarkFailed(workItem, incrementRetry: true);
                        await dbContext.SaveChangesAsync();
                    }
                });

                executionTasks.Add(task);

                if (!currentConcurrency.TryGetValue(providerConfig.ProviderKey, out var count))
                {
                    currentConcurrency[providerConfig.ProviderKey] = 1;
                }
                else
                {
                    currentConcurrency[providerConfig.ProviderKey] = count + 1;
                }
            }
        }

        await Task.WhenAll(executionTasks);

        return true;
    }

    private static bool CanExecuteNow(EnrichmentProviderConfig providerConfig, Dictionary<string, DateTime> lastAttempts, Dictionary<string, int> inProgressCounts)
    {
        if (providerConfig.AllowParallelCalls == false &&
            inProgressCounts.TryGetValue(providerConfig.ProviderKey, out var inProgress1) &&
            inProgress1 > 0)
        {
            return false;
        }

        if (providerConfig.MaxConcurrency.HasValue &&
            inProgressCounts.TryGetValue(providerConfig.ProviderKey, out var inProgress2) &&
            inProgress2 >= providerConfig.MaxConcurrency.Value)
        {
            return false;
        }

        if (providerConfig.MinCallIntervalMs.HasValue &&
            providerConfig.MinCallIntervalMs.Value > 0 &&
            lastAttempts.TryGetValue(providerConfig.ProviderKey, out var lastAttempt) &&
            (DateTime.UtcNow - lastAttempt).TotalMilliseconds < providerConfig.MinCallIntervalMs.Value)
        {
            return false;
        }

        return true;
    }

    private async Task ExecuteWorkItemAsync(EnrichmentWorkItem workItem, EnrichmentProviderConfig providerConfig)
    {
        var provider = providerResolver.Resolve(workItem.ProviderKey);
        if (provider is null)
        {
            Log.Warning($"Provider {workItem.ProviderKey} not found; marking work item {workItem.Id} as blocked");
            workItemService.MarkBlocked(workItem);
            await dbContext.SaveChangesAsync();
            return;
        }

        var entity = await LoadEntityAsync(workItem.EntityType, workItem.EntityId);
        if (entity is null)
        {
            Log.Warning($"Entity {workItem.EntityType}#{workItem.EntityId} not found; marking work item {workItem.Id} as completed");
            workItemService.MarkCompleted(workItem);
            await dbContext.SaveChangesAsync();
            return;
        }

        var shouldEnrich = await provider.ShouldEnrichAsync(entity);
        if (!shouldEnrich)
        {
            workItemService.MarkCompleted(workItem);
            await dbContext.SaveChangesAsync();
            return;
        }

        var quotaAllowed = await quotaService.TryConsumeAsync(providerConfig);
        if (!quotaAllowed)
        {
            Log.Information($"Quota exhausted for provider {workItem.ProviderKey}; work item {workItem.Id} remains pending");
            await dbContext.SaveChangesAsync();
            return;
        }

        workItemService.MarkInProgress(workItem);
        await dbContext.SaveChangesAsync();

        var sw = Stopwatch.StartNew();
        var result = await provider.EnrichAsync(entity);
        sw.Stop();

        var attempt = new EnrichmentProviderAttempt
        {
            WorkItemId = workItem.Id,
            ProviderKey = workItem.ProviderKey,
            EntityType = workItem.EntityType,
            EntityId = workItem.EntityId,
            Success = result.Success,
            ErrorCategory = result.ErrorCategory,
            ErrorMessage = result.ErrorMessage,
            DurationMs = (int)sw.ElapsedMilliseconds,
            ResponsePayload = result.Success && result.FieldChanges.Count > 0 ? JsonHelper.Serialize(result.FieldChanges) : null,
        };

        dbContext.EnrichmentProviderAttempts!.Add(attempt);

        if (result.Success)
        {
            if (result.NoChanges || result.FieldChanges.Count == 0)
            {
                workItemService.MarkCompleted(workItem);
            }
            else
            {
                await auditService.WriteAuditAsync(workItem.ProviderKey, workItem.EntityType, workItem.EntityId, result.FieldChanges);
                workItemService.MarkCompleted(workItem);
            }
        }
        else
        {
            if (result.ErrorCategory == EnrichmentErrorCategory.AuthInvalid ||
                result.ErrorCategory == EnrichmentErrorCategory.AuthMissing ||
                result.ErrorCategory == EnrichmentErrorCategory.BadInput)
            {
                workItemService.MarkBlocked(workItem);
            }
            else if (result.ErrorCategory == EnrichmentErrorCategory.RateLimited)
            {
                // Leave as pending; will retry later
            }
            else
            {
                workItemService.MarkFailed(workItem, incrementRetry: true);
            }
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task<object?> LoadEntityAsync(string entityType, int entityId)
    {
        var entityTypeObj = dbContext.Model.GetEntityTypes().FirstOrDefault(e => e.ClrType.Name == entityType)?.ClrType;
        if (entityTypeObj is null)
        {
            return null;
        }

        var dbSet = dbContext.GetType().GetProperty(entityType + "s")?.GetValue(dbContext);
        if (dbSet is null)
        {
            return null;
        }

        var findMethod = dbSet.GetType().GetMethod("FindAsync", new[] { typeof(object[]) });
        if (findMethod is null)
        {
            return null;
        }

        if (findMethod.Invoke(dbSet, new object[] { new object[] { entityId } }) is not Task<object> task)
        {
            return null;
        }

        return await task;
    }
}
