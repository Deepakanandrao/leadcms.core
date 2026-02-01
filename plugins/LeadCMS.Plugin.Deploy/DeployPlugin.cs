// <copyright file="DeployPlugin.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.Deployments.Interfaces;
using LeadCMS.Interfaces;
using LeadCMS.Plugin.Deploy.Configuration;
using LeadCMS.Plugin.Deploy.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

        // Register the deployment service as singleton since AzureDevOpsClient is thread-safe
        // This allows connection reuse across requests for better performance
        services.AddSingleton(Configuration);
        services.AddSingleton<AzureDevOpsClient>(sp =>
        {
            var logger = sp.GetService<ILogger<AzureDevOpsClient>>();
            return new AzureDevOpsClient(Configuration.AzureDevOps, logger);
        });
        services.AddSingleton<IDeploymentService, AzureDevOpsDeploymentService>();
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetCapabilities()
    {
        yield return "Deployment";
    }
}
