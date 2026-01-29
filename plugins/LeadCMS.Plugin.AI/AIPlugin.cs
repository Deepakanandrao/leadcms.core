// <copyright file="AIPlugin.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.AIAssistance.Interfaces;
using LeadCMS.Core.AIAssistance.Services;
using LeadCMS.Plugins.AI.Configuration;
using LeadCMS.Plugins.AI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Plugins.AI;

public class AIPlugin : IPlugin, ICapabilityProvider
{
    public static PluginConfig Configuration { get; private set; } = new PluginConfig();

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var pluginConfig = configuration.Get<PluginConfig>();

        if (pluginConfig != null)
        {
            Configuration = pluginConfig;
        }

        var hasApiKey = !string.IsNullOrWhiteSpace(Configuration.OpenAI.ApiKey) &&
            !Configuration.OpenAI.ApiKey.StartsWith("$", StringComparison.Ordinal);

        if (hasApiKey)
        {
            services.AddSingleton<IAIProviderService>(_ => new OpenAIProviderService(Configuration.OpenAI));
        }
        else
        {
            services.AddSingleton<IAIProviderService, NullAIProviderService>();
        }
    }

    public IEnumerable<string> GetCapabilities()
    {
        return new[] { "AIAssistance" };
    }
}
