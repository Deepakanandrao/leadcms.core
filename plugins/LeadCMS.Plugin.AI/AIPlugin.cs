// <copyright file="AIPlugin.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Plugin.AI.Configuration;
using LeadCMS.Plugin.AI.Interfaces;
using LeadCMS.Plugin.AI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Plugin.AI;

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

        // Register AI services
        services.AddSingleton<ITextGenerationService, TextGenerationService>();
        services.AddSingleton<IImageGenerationService, ImageGenerationService>();
        services.AddScoped<IContentAITranslationService, ContentAITranslationService>();
        services.AddScoped<IContentGenerationService, ContentGenerationService>();
        services.AddScoped<IEmailTemplateAITranslationService, EmailTemplateAITranslationService>();
        services.AddScoped<IEmailTemplateGenerationService, EmailTemplateGenerationService>();
    }

    public IEnumerable<string> GetCapabilities()
    {
        return new[] { "AIAssistance" };
    }
}
