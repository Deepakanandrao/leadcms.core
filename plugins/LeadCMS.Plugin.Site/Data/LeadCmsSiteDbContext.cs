// <copyright file="LeadCmsSiteDbContext.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Interfaces;
using LeadCMS.Plugin.Site.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LeadCMS.Plugin.Site.Data;

public class LeadCmsSiteDbContext : PluginDbContextBase
{
    public LeadCmsSiteDbContext()
        : base()
    {
    }

    public LeadCmsSiteDbContext(DbContextOptions<PgDbContext> options, IConfiguration configuration, IHttpContextHelper httpContextHelper)
        : base(options, configuration, httpContextHelper)
    {
    }

    protected override bool ExcludeBaseEntitiesFromMigrations => true;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        EmailGroupData.Seed(builder);

        EmailTemplateData.Seed(builder);

        base.OnModelCreating(builder);
    }    
}