// <copyright file="SendgridDbContext.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Interfaces;
using LeadCMS.Plugin.SendGrid.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LeadCMS.Plugin.SendGrid.Data;

public class SendgridDbContext : PluginDbContextBase
{
    public SendgridDbContext()
        : base()
    {
    }

    public SendgridDbContext(DbContextOptions<PgDbContext> options, IConfiguration configuration, IHttpContextHelper httpContextHelper)
        : base(options, configuration, httpContextHelper)
    {
    }

    public DbSet<SendgridEvent>? SendgridEvents { get; set; }
}