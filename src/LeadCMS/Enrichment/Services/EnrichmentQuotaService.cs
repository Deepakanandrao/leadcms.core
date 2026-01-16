// <copyright file="EnrichmentQuotaService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Enrichment.Interfaces;
using LeadCMS.Entities;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Enrichment.Services;

public class EnrichmentQuotaService : IEnrichmentQuotaService
{
    private readonly PgDbContext dbContext;

    public EnrichmentQuotaService(PgDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<bool> TryConsumeAsync(EnrichmentProviderConfig providerConfig)
    {
        // Fixed-window simple enforcement; further polish can be added later.
        var now = DateTime.UtcNow;

        if (providerConfig.HourlyQuota.HasValue && !await TryConsumeWindow(providerConfig.ProviderKey, EnrichmentQuotaWindow.Hourly, providerConfig.HourlyQuota.Value, now))
        {
            return false;
        }

        if (providerConfig.DailyQuota.HasValue && !await TryConsumeWindow(providerConfig.ProviderKey, EnrichmentQuotaWindow.Daily, providerConfig.DailyQuota.Value, now))
        {
            return false;
        }

        if (providerConfig.MonthlyQuota.HasValue && !await TryConsumeWindow(providerConfig.ProviderKey, EnrichmentQuotaWindow.Monthly, providerConfig.MonthlyQuota.Value, now))
        {
            return false;
        }

        return true;
    }

    private async Task<bool> TryConsumeWindow(string providerKey, EnrichmentQuotaWindow window, int limit, DateTime now)
    {
        var windowStart = window switch
        {
            EnrichmentQuotaWindow.Hourly => new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc),
            EnrichmentQuotaWindow.Daily => new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc),
            EnrichmentQuotaWindow.Monthly => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(window)),
        };

        var usage = await dbContext.EnrichmentQuotaUsages!
            .FirstOrDefaultAsync(q => q.ProviderKey == providerKey && q.WindowType == window && q.WindowStart == windowStart);

        if (usage is null)
        {
            usage = new EnrichmentQuotaUsage
            {
                ProviderKey = providerKey,
                WindowType = window,
                WindowStart = windowStart,
                UsageCount = 1,
            };

            dbContext.EnrichmentQuotaUsages!.Add(usage);
        }
        else
        {
            if (usage.UsageCount >= limit)
            {
                return false;
            }

            usage.UsageCount += 1;
        }

        return true;
    }
}
