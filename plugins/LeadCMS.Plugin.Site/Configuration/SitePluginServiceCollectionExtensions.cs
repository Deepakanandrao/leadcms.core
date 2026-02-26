// <copyright file="SitePluginServiceCollectionExtensions.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Plugin.Site.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LeadCMS.Plugin.Site.Configuration;

/// <summary>
/// Site plugin registration helpers for custom plugin reuse.
/// </summary>
public static class SitePluginServiceCollectionExtensions
{
    /// <summary>
    /// Registers Site plugin settings accessor.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration root.</param>
    public static IServiceCollection AddSitePluginSettingsAccessor(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.Get<PluginSettings>() ?? new PluginSettings();
        services.TryAddSingleton<ISitePluginSettingsAccessor>(new SitePluginSettingsAccessor(settings));
        return services;
    }

    /// <summary>
    /// Registers Site lead notification abstractions and default implementation.
    /// </summary>
    /// <param name="services">Service collection.</param>
    public static IServiceCollection AddSiteLeadNotificationServices(this IServiceCollection services)
    {
        services.TryAddScoped<ILeadNotificationMessageBuilder, DefaultLeadNotificationMessageBuilder>();
        services.TryAddScoped<ILeadNotificationService, LeadNotificationService>();
        return services;
    }

    /// <summary>
    /// Registers Site subscription token service.
    /// </summary>
    /// <param name="services">Service collection.</param>
    public static IServiceCollection AddSiteSubscriptionTokenService(this IServiceCollection services)
    {
        services.TryAddSingleton<ISubscriptionTokenService>(provider =>
        {
            var settingsAccessor = provider.GetRequiredService<ISitePluginSettingsAccessor>();
            return new SubscriptionTokenService(settingsAccessor.Settings.SubscriptionTokenSecret);
        });

        return services;
    }

    /// <summary>
    /// Registers all reusable Site plugin services required for contact and subscription features.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration root.</param>
    public static IServiceCollection AddSiteCoreServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSitePluginSettingsAccessor(configuration);
        services.AddSiteLeadNotificationServices();
        services.AddSiteSubscriptionTokenService();
        return services;
    }
}

/// <summary>
/// Provides strongly typed Site plugin settings.
/// </summary>
public interface ISitePluginSettingsAccessor
{
    /// <summary>
    /// Gets configured plugin settings.
    /// </summary>
    PluginSettings Settings { get; }
}

internal sealed class SitePluginSettingsAccessor(PluginSettings settings) : ISitePluginSettingsAccessor
{
    public PluginSettings Settings { get; } = settings;
}
