// <copyright file="SitePlugin.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Interfaces;
using LeadCMS.Plugin.Site.Configuration;
using LeadCMS.Plugin.Site.Data;
using LeadCMS.Plugin.Site.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Plugin.Site;

/// <summary>
/// Site plugin providing website functionality including contact forms, subscriptions, and lead capture.
/// </summary>
public class SitePlugin : IPlugin, ICapabilityProvider
{
    public static PluginSettings Settings { get; private set; } = new PluginSettings();

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var pluginSettings = configuration.Get<PluginSettings>();

        if (pluginSettings != null)
        {
            Settings = pluginSettings;
        }

        services.AddScoped<PluginDbContextBase, LeadCmsSiteDbContext>();
        services.AddScoped<LeadCmsSiteDbContext, LeadCmsSiteDbContext>();
        services.AddScoped<ILeadNotificationService, LeadNotificationService>();
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetCapabilities()
    {
        yield return "Site";
    }
}