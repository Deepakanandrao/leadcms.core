// <copyright file="ChangeLogTask.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Reflection;
using LeadCMS.Configuration;
using LeadCMS.Data;
using LeadCMS.DataAnnotations;
using LeadCMS.Entities;
using LeadCMS.Services;

namespace LeadCMS.Tasks;

public abstract class ChangeLogTask : BaseTask
{
    protected readonly PgDbContext dbContext;

    protected readonly IEnumerable<PluginDbContextBase> pluginDbContexts;

    private readonly HashSet<Type> loggedTypes;

    protected ChangeLogTask(string configKey, IConfiguration configuration, PgDbContext dbContext, IEnumerable<PluginDbContextBase> pluginDbContexts, TaskStatusService taskStatusService)
        : base(configKey, configuration, taskStatusService)
    {
        this.dbContext = dbContext;
        this.pluginDbContexts = pluginDbContexts;
        loggedTypes = GetTypes(dbContext);

        var config = configuration.GetSection(configKey)!.Get<TaskWithBatchConfig>();

        if (config is not null)
        {
            ChangeLogBatchSize = config.BatchSize;
        }
        else
        {
            throw new MissingConfigurationException($"The specified configuration section for the provided configKey {configKey} could not be found in the settings file.");
        }

        foreach (var pt in pluginDbContexts)
        {
            var lt = GetTypes(pt);
            loggedTypes.UnionWith(lt);
        }
    }

    public int ChangeLogBatchSize { get; private set; }

    public override Task<bool> Execute(TaskExecutionLog currentJob)
    {
        int totalProcessed = 0;
        var processedTypes = new List<string>();
        var taskResults = new List<string>();

        foreach (var loggedType in loggedTypes)
        {
            var taskAndEntity = Name + "_" + loggedType.Name;

            if (IsPreviousTaskInProgress(taskAndEntity))
            {
                continue;
            }

            var changeLogBatch = GetNextOrFailedChangeLogBatch(taskAndEntity, loggedType);
            int typeProcessed = 0;

            while (changeLogBatch is not null && changeLogBatch!.Any())
            {
                var taskLog = AddChangeLogTaskLogRecord(taskAndEntity, changeLogBatch!.First().Id, changeLogBatch!.Last().Id);

                try
                {
                    var batchResult = ExecuteLogTask(changeLogBatch!, loggedType);
                    if (!string.IsNullOrWhiteSpace(batchResult))
                    {
                        taskResults.Add(batchResult);
                    }

                    typeProcessed += changeLogBatch.Count;
                    totalProcessed += changeLogBatch.Count;

                    UpdateChangeLogTaskLogRecord(taskLog, changeLogBatch!.Count, TaskExecutionState.Completed);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Error occurred when executing task {taskAndEntity}");

                    UpdateChangeLogTaskLogRecord(taskLog, 0, TaskExecutionState.Failed);
                    currentJob.Result = $"Failed processing {loggedType.Name}: {ex.Message}";

                    return Task.FromResult(false);
                }

                changeLogBatch = GetNextOrFailedChangeLogBatch(taskAndEntity, loggedType);
            }

            if (typeProcessed > 0)
            {
                processedTypes.Add($"{loggedType.Name}({typeProcessed})");
            }
        }

        // Use task-specific results if provided, otherwise use default summary
        if (taskResults.Count > 0)
        {
            currentJob.Result = string.Join("; ", taskResults);
        }
        else
        {
            currentJob.Result = totalProcessed > 0
                ? $"Processed {totalProcessed} changes across {processedTypes.Count} entity types: {string.Join(", ", processedTypes)}"
                : "No changes to process";
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// Executes the task logic for a batch of change log entries.
    /// </summary>
    /// <param name="nextBatch">The batch of change log entries to process.</param>
    /// <param name="loggedType">The entity type being processed.</param>
    /// <returns>A summary string describing what was done in this batch, or null/empty for default summary.</returns>
    protected abstract string? ExecuteLogTask(List<ChangeLog> nextBatch, Type loggedType);

    protected HashSet<Type> GetTypes(PgDbContext context)
    {
        var res = new HashSet<Type>();

        var types = context.Model.GetEntityTypes();

        foreach (var type in types.Select(type => type.ClrType))
        {
            if (type != null && IsChangeLogAttribute(type) && IsTypeSupported(type))
            {
                res.Add(type);
            }
        }

        return res;
    }

    protected abstract bool IsTypeSupported(Type type);

    protected virtual int GetMinLogId(ChangeLogTaskLog lastProcessedTask, Type loggedType)
    {
        return lastProcessedTask.ChangeLogIdMax + 1;
    }

    private bool IsChangeLogAttribute(Type type)
    {
        return type.GetCustomAttributes<SupportsChangeLogAttribute>().Any();
    }

    private bool IsPreviousTaskInProgress(string name)
    {
        var inProgressCount = dbContext.ChangeLogTaskLogs!.Count(c => c.TaskName == name && c.State == TaskExecutionState.InProgress);

        return inProgressCount > 0;
    }

    private void UpdateChangeLogTaskLogRecord(ChangeLogTaskLog taskLog, int changesProcessed, TaskExecutionState state)
    {
        taskLog.ChangesProcessed = changesProcessed;
        taskLog.State = state;
        taskLog.End = DateTime.UtcNow;

        dbContext.SaveChanges();
    }

    private ChangeLogTaskLog AddChangeLogTaskLogRecord(string taskName, int minLogId, int maxLogId)
    {
        var changeLogTaskLogEntry = new ChangeLogTaskLog()
        {
            TaskName = taskName,
            Start = DateTime.UtcNow,
            State = TaskExecutionState.InProgress,
            ChangeLogIdMin = minLogId,
            ChangeLogIdMax = maxLogId,
        };

        dbContext.ChangeLogTaskLogs!.Add(changeLogTaskLogEntry);
        dbContext.SaveChanges();

        return changeLogTaskLogEntry;
    }

    private List<ChangeLog> GetNextOrFailedChangeLogBatch(string taskName, Type loggedType)
    {
        var minLogId = 1;

        var lastProcessedTask = dbContext.ChangeLogTaskLogs!.Where(c => c.TaskName == taskName).OrderByDescending(t => t.Id).FirstOrDefault();

        if (lastProcessedTask is not null && lastProcessedTask.State == TaskExecutionState.Failed)
        {
            var failedTaskCount = dbContext.ChangeLogTaskLogs!.Count(c => c.TaskName == taskName && c.ChangeLogIdMin == lastProcessedTask.ChangeLogIdMin);
            if (failedTaskCount > 0 && failedTaskCount <= RetryCount)
            {
                // If this is a retry, get the same minId of last processed task to re-execute the same batch.
                minLogId = lastProcessedTask.ChangeLogIdMin;
            }
            else
            {
                // If all retries are completed then discontinue.
                Log.Error($"Error in executing task {taskName} for entity {loggedType.Name} from Id {lastProcessedTask.ChangeLogIdMin} to {lastProcessedTask.ChangeLogIdMax}");

                return Enumerable.Empty<ChangeLog>().ToList();
            }
        }
        else if (lastProcessedTask is not null && lastProcessedTask.State == TaskExecutionState.Completed)
        {
            minLogId = GetMinLogId(lastProcessedTask, loggedType);
        }

        // 3000000 - tests show that limit of 3 millions records is optimal to split change_log table into the parts with acceptable performance
        var changeLogList = dbContext.ChangeLogs!.Where(c => c.Id >= minLogId && c.Id < minLogId + 3000000 && c.ObjectType == loggedType.Name).OrderBy(b => b.Id).Take(ChangeLogBatchSize).ToList();

        return changeLogList;
    }
}