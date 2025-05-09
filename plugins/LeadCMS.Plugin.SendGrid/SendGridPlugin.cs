// <copyright file="SendGridPlugin.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Interfaces;
using LeadCMS.Plugin.SendGrid.Configuration;
using LeadCMS.Plugin.SendGrid.Data;
using LeadCMS.Plugin.SendGrid.Tasks;
using LeadCMS.SendGrid.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Plugin.SendGrid;

public class SendGridPlugin : IPlugin
{
    public static PluginConfig Configuration { get; private set; } = new PluginConfig();

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var pluginConfig = configuration.Get<PluginConfig>();

        if (pluginConfig != null)
        {
            Configuration = pluginConfig;
        }

        services.AddScoped<PluginDbContextBase, SendgridDbContext>();
        services.AddScoped<SendgridDbContext, SendgridDbContext>();

        services.AddScoped<ITask, SyncSuppressionsTask>();
        services.AddScoped<ITask, SyncActivityLogTask>();
    }
}