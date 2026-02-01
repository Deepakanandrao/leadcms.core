// <copyright file="DeployPlugin.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.Deployments.Interfaces;
using LeadCMS.Interfaces;
using LeadCMS.Plugin.Deploy.Configuration;
using LeadCMS.Plugin.Deploy.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Plugin.Deploy;

/// <summary>
/// Plugin for deployment capabilities via Azure DevOps.
/// </summary>
public class DeployPlugin : IPlugin, ICapabilityProvider
{
    /// <summary>
    /// Gets the plugin configuration.
    /// </summary>
    public static DeployPluginSettings Configuration { get; private set; } = new DeployPluginSettings();

    /// <inheritdoc/>
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var pluginConfig = configuration.Get<DeployPluginSettings>();

        if (pluginConfig != null)
        {
            Configuration = pluginConfig;
        }

        // Register the deployment service
        services.AddSingleton(Configuration);
        services.AddScoped<IDeploymentService, AzureDevOpsDeploymentService>();
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetCapabilities()
    {
        yield return "Deployment";
    }
}
