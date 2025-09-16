// <copyright file="ConfigController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Globalization;
using LeadCMS.Configuration;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Infrastructure;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeadCMS.Controllers;

[ApiController]
[Route("api/config")]
public class ConfigController : ControllerBase
{
    private readonly IConfiguration configuration;
    private readonly IServiceProvider serviceProvider;
    private readonly ISettingService settingService;
    private readonly IHttpContextHelper httpContextHelper;
    private readonly ICapabilityService capabilityService;

    public ConfigController(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ISettingService settingService,
        IHttpContextHelper httpContextHelper,
        ICapabilityService capabilityService)
    {
        this.configuration = configuration;
        this.serviceProvider = serviceProvider;
        this.settingService = settingService;
        this.httpContextHelper = httpContextHelper;
        this.capabilityService = capabilityService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ConfigDto>> GetConfig()
    {
        var jwtConfig = configuration.GetSection("Jwt").Get<JwtConfig>() ?? new JwtConfig();
        var azureAdConfig = configuration.GetSection("AzureAd").Get<AzureADConfig>() ?? new AzureADConfig();
        var entitiesConfig = configuration.GetSection("Entities").Get<EntitiesConfig>() ?? new EntitiesConfig();
        var supportedLanguagesConfig = configuration.GetSection("SupportedLanguages").Get<string[]>() ?? Array.Empty<string>();

        var authMethods = new List<string>();
        if (jwtConfig.IsInitialized())
        {
            authMethods.Add("Local");
        }

        if (azureAdConfig.IsInitialized())
        {
            authMethods.Add("AzureAD");
        }

        MsalConfigDto? msalConfig = null;
        if (azureAdConfig.IsInitialized())
        {
            msalConfig = new MsalConfigDto
            {
                ClientId = azureAdConfig.ClientId,
                Authority = azureAdConfig.Authority,
                RedirectUri = "/auth/callback",
            };
        }

        var allEntities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var scope = serviceProvider.CreateScope())
        {
            var mainDbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();
            foreach (var entityType in mainDbContext.Model.GetEntityTypes().Select(e => e.ClrType))
            {
                if (entityType.Namespace != null && entityType.Namespace.Contains("LeadCMS.Entities"))
                {
                    allEntities.Add(entityType.Name);
                }
            }

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
            availableEntities = allEntities.Except(entitiesConfig.Exclude, StringComparer.OrdinalIgnoreCase);
        }

        var languages = supportedLanguagesConfig
            .Select(code => new LanguageDto
            {
                Code = code,
                Name = CultureInfo.GetCultures(CultureTypes.AllCultures)
                    .FirstOrDefault(c => c.Name == code)?.DisplayName ?? code,
            })
            .ToList();

        // Only return these two settings, with user override if user is known
        var hardcodedSettingKeys = new[] { "PreviewUrlTemplate", "LivePreviewUrlTemplate" };
        string? userId = null;
        if (User?.Identity?.IsAuthenticated == true)
        {
            userId = await httpContextHelper.GetCurrentUserIdAsync();
        }

        var settings = await settingService.GetSettingsByKeysAsync(hardcodedSettingKeys, userId);

        // Get DefaultLanguage from ApiSettings section
        var apiSettingsSection = configuration.GetSection("ApiSettings");
        var defaultLanguage = apiSettingsSection["DefaultLanguage"] ?? "en";

        var dynamicModules = PluginManager.GetAllDynamicModules();

        // Get all capabilities from registered providers
        var capabilities = capabilityService.GetAllCapabilities();

        var configDto = new ConfigDto
        {
            Auth = new AuthConfigDto
            {
                Methods = authMethods,
                Msal = msalConfig,
            },
            Entities = availableEntities,
            Languages = languages,
            Settings = settings,
            DefaultLanguage = defaultLanguage,
            Modules = dynamicModules,
            Capabilities = capabilities,
        };

        return Ok(configDto);
    }
}

public class ConfigDto
{
    public AuthConfigDto Auth { get; set; } = new AuthConfigDto();

    public IEnumerable<string> Entities { get; set; } = Array.Empty<string>();

    public List<LanguageDto> Languages { get; set; } = new List<LanguageDto>();

    public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();

    public string DefaultLanguage { get; set; } = "en";

    public List<DynamicModuleDto>? Modules { get; set; }

    public IEnumerable<string> Capabilities { get; set; } = Array.Empty<string>();
}

public class AuthConfigDto
{
    public List<string> Methods { get; set; } = new List<string>();

    public MsalConfigDto? Msal { get; set; }
}

public class MsalConfigDto
{
    public string ClientId { get; set; } = string.Empty;

    public string Authority { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = string.Empty;
}

public class LanguageDto
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
