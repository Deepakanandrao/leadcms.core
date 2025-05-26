// <copyright file="ConfigController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Configuration;
using LeadCMS.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeadCMS.Controllers;

[ApiController]
[Route("api/config")]
public class ConfigController : ControllerBase
{
    private readonly IConfiguration configuration;
    private readonly IServiceProvider serviceProvider;

    public ConfigController(IConfiguration configuration, IServiceProvider serviceProvider)
    {
        this.configuration = configuration;
        this.serviceProvider = serviceProvider;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult GetConfig()
    {
        // Use AppSettings helpers for config parsing
        var jwtConfig = configuration.GetSection("Jwt").Get<JwtConfig>() ?? new JwtConfig();
        var azureAdConfig = configuration.GetSection("AzureAd").Get<AzureADConfig>() ?? new AzureADConfig();
        var entitiesConfig = configuration.GetSection("Entities").Get<EntitiesConfig>() ?? new EntitiesConfig();
        var supportedLanguagesConfig = configuration.GetSection("SupportedLanguages").Get<string[]>() ?? Array.Empty<string>();

        // Auth methods
        var authMethods = new List<string>();
        if (jwtConfig.IsInitialized())
        {
            authMethods.Add("Local");
        }

        if (azureAdConfig.IsInitialized())
        {
            authMethods.Add("AzureAD");
        }

        // MSAL config
        var msalConfig = new
        {
            clientId = azureAdConfig.ClientId,
            authority = azureAdConfig.Authority,
            redirectUri = "/auth/callback", // relative path only
        };

        // Entities
        var allEntities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var scope = serviceProvider.CreateScope())
        {
            // Main context
            var mainDbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();
            foreach (var entityType in mainDbContext.Model.GetEntityTypes().Select(e => e.ClrType))
            {
                if (entityType.Namespace != null && entityType.Namespace.Contains("LeadCMS.Entities"))
                {
                    allEntities.Add(entityType.Name);
                }
            }

            // Plugin contexts
            var pluginDbContexts = scope.ServiceProvider.GetServices<PluginDbContextBase>();
            foreach (var pluginContext in pluginDbContexts)
            {
                foreach (var entityType in pluginContext.Model.GetEntityTypes().Select(e => e.ClrType))
                {
                    allEntities.Add(entityType.Name);
                }
            }
        }

        IEnumerable<string> availableEntities;
        if (entitiesConfig.Include != null && entitiesConfig.Include.Length > 0)
        {
            availableEntities = entitiesConfig.Include;
        }
        else
        {
            availableEntities = allEntities.Except(entitiesConfig.Exclude, System.StringComparer.OrdinalIgnoreCase);
        }

        // SupportedLanguages
        var languages = supportedLanguagesConfig
            .Select(code => new
            {
                code,
                name = System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.AllCultures)
                    .FirstOrDefault(c => c.Name == code)?.DisplayName ?? code,
            })
            .ToList();

        var result = new
        {
            auth = new
            {
                methods = authMethods,
                msal = msalConfig,
            },
            entities = availableEntities,
            supportedLanguages = languages,
        };
        return Ok(result);
    }
}
