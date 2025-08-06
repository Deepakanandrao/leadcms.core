// <copyright file="SmsDbContext.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Plugin.Sms.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LeadCMS.Plugin.Sms.Data;

public class SmsDbContext : PluginDbContextBase
{
    public SmsDbContext()
        : base()
    {
    }

    public SmsDbContext(DbContextOptions<PgDbContext> options, IConfiguration configuration, IHttpContextHelper httpContextHelper)
        : base(options, configuration, httpContextHelper)
    {
    }

    public DbSet<SmsLog>? SmsLogs { get; set; }
}