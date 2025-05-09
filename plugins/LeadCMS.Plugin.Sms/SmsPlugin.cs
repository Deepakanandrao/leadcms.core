// <copyright file="SmsPlugin.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Plugin.Sms.Configuration;
using LeadCMS.Plugin.Sms.Data;
using LeadCMS.Plugin.Sms.Services;
using LeadCMS.Plugin.Sms.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Plugin.Sms;

public class SmsPlugin : IPlugin
{
    public static PluginConfig Configuration { get; private set; } = new PluginConfig();

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var pluginConfig = configuration.Get<PluginConfig>();

        if (pluginConfig != null)
        {
            Configuration = pluginConfig;
        }

        services.AddScoped<PluginDbContextBase, SmsDbContext>();
        services.AddScoped<SmsDbContext, SmsDbContext>();

        services.AddSingleton<ISmsService, SmsService>();

        services.AddScoped<ITask, SyncSmsLogTask>();
    }
}