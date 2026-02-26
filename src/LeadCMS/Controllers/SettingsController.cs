// <copyright file="SettingsController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Infrastructure;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Controllers;

[Authorize]
[Route("api/[controller]")]
public class SettingsController : BaseControllerWithImport<Setting, SettingCreateDto, SettingUpdateDto, SettingDetailsDto, SettingImportDto>
{
    private readonly ISettingService settingService;
    private readonly UserManager<User> userManager;
    private readonly ISettingsEnrichmentService settingsEnrichmentService;

    public SettingsController(
        PgDbContext dbContext,
        IMapper mapper,
        EsDbContext esDbContext,
        QueryProviderFactory<Setting> queryProviderFactory,
        ISettingService settingService,
        UserManager<User> userManager,
        ISettingsEnrichmentService settingsEnrichmentService,
        ISyncService syncService)
        : base(dbContext, mapper, esDbContext, queryProviderFactory, syncService)
    {
        this.settingService = settingService;
        this.userManager = userManager;
        this.settingsEnrichmentService = settingsEnrichmentService;
    }

    /// <summary>
    /// Create or update a setting. Enforces uniqueness on (Key, UserId, Language).
    /// If a setting with the same Key, UserId and Language already exists, it is updated instead.
    /// Language-specific and user-specific settings are only saved if their value
    /// differs from the global (language-neutral, system-level) setting.
    /// Metadata (Required, Type, Description) is populated from plugin definitions and cannot be set by clients.
    /// </summary>
    /// <param name="value">Setting to create or update.</param>
    /// <returns>Created or updated setting.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<SettingDetailsDto>> Post([FromBody] SettingCreateDto value)
    {
        if (string.IsNullOrEmpty(value.UserId))
        {
            await settingService.SetSystemSettingAsync(value.Key, value.Value, value.Language);
        }
        else
        {
            await settingService.SetUserSettingAsync(value.Key, value.Value, value.UserId);
        }

        var setting = await dbContext.Settings!
            .Where(s => s.Key == value.Key
                && s.UserId == (string.IsNullOrEmpty(value.UserId) ? null : value.UserId)
                && s.Language == value.Language)
            .FirstOrDefaultAsync();

        // If setting was not saved (value matches global), return the global setting
        if (setting == null)
        {
            setting = await dbContext.Settings!
                .Where(s => s.Key == value.Key && s.UserId == null && s.Language == null)
                .FirstOrDefaultAsync();
        }

        var settingDto = mapper.Map<SettingDetailsDto>(setting);
        return CreatedAtAction(nameof(GetOne), new { id = setting!.Id }, settingDto);
    }

    /// <summary>
    /// Get all system-level settings enriched with default values from appsettings (Admin only).
    /// Database settings take precedence over appsettings defaults.
    /// Optionally pass a language code to include language-specific overrides.
    /// Language matching is fuzzy: "ru" matches "ru-RU" and vice versa.
    /// </summary>
    /// <param name="language">Optional language code (e.g. "en", "ru-RU"). When provided, general settings are returned with language-specific overrides merged on top using fuzzy matching.</param>
    /// <returns>List of system-level settings enriched with defaults.</returns>
    [HttpGet("system")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<SettingDetailsDto>>> GetSystemSettings([FromQuery] string? language = null)
    {
        // Get all general (language-neutral) system settings
        var generalSettings = await dbContext.Settings!
            .Where(s => s.UserId == null && s.Language == null)
            .ToListAsync();

        // Enrich with default values using the enrichment service (adds missing keys from config/plugins)
        await settingsEnrichmentService.EnrichWithAllKnownSettingsAsync(generalSettings);

        // Build the result starting from enriched general settings
        var resultByKey = generalSettings.ToDictionary(
            s => s.Key,
            s =>
            {
                var pluginDef = settingsEnrichmentService.GetSettingDefinitions()
                    .FirstOrDefault(d => d.Key == s.Key);

                // For virtual settings (not from DB), use plugin metadata
                if (s.Id == 0 && pluginDef != null)
                {
                    s.Required = pluginDef.Required;
                    s.Type = pluginDef.Type;
                    s.Description = pluginDef.Description;
                }

                return s;
            });

        // If a language was requested, overlay language-specific values using fuzzy matching
        if (!string.IsNullOrWhiteSpace(language))
        {
            var langSettings = await dbContext.Settings!
                .Where(s => s.UserId == null && s.Language != null)
                .ToListAsync();

            // Filter to only settings that match the requested language (exact or family match)
            var matchingLangSettings = langSettings
                .Where(s => SettingListHelper.LanguageFamilyMatches(s.Language, language) ||
                            string.Equals(s.Language, language, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Group by key and pick best match per key
            foreach (var group in matchingLangSettings.GroupBy(s => s.Key))
            {
                var best = SettingListHelper.PickBestLanguageMatch(group.ToList(), language);
                if (best != null && resultByKey.TryGetValue(best.Key, out var existing))
                {
                    // Override value and metadata with language-specific version
                    existing.Value = best.Value;
                    existing.Language = best.Language;
                    if (best.Id != 0)
                    {
                        existing.Id = best.Id;
                    }
                }
                else if (best != null)
                {
                    resultByKey[best.Key] = best;
                }
            }
        }

        var settingDtos = mapper.Map<List<SettingDetailsDto>>(resultByKey.Values.ToList());
        return Ok(settingDtos);
    }

    /// <summary>
    /// Get a specific system-level setting by key (Admin only).
    /// Optionally pass a language code for language-specific override resolution with fuzzy matching.
    /// </summary>
    /// <param name="key">Setting key.</param>
    /// <param name="language">Optional language code for fuzzy language matching.</param>
    /// <returns>System-level setting details.</returns>
    [HttpGet("system/{key}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SettingDetailsDto>> GetSystemSetting(string key, [FromQuery] string? language = null)
    {
        var setting = await settingService.FindSystemSettingAsync(key, language);

        if (setting == null)
        {
            return NotFound($"System setting with key '{key}' not found.");
        }

        var settingDto = mapper.Map<SettingDetailsDto>(setting);
        return Ok(settingDto);
    }

    /// <summary>
    /// Create or update a system-level setting (Admin only).
    /// </summary>
    /// <param name="key">Setting key.</param>
    /// <param name="value">Setting value.</param>
    /// <param name="language">Optional language code for language-specific override.</param>
    /// <returns>Updated setting details.</returns>
    [HttpPut("system/{key}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SettingDetailsDto>> SetSystemSetting(
        string key,
        [FromQuery] string? value,
        [FromQuery] string? language = null)
    {
        await settingService.SetSystemSettingAsync(key, value, language);

        var setting = await dbContext.Settings!
            .Where(s => s.Key == key && s.UserId == null && s.Language == language)
            .FirstOrDefaultAsync();

        // If setting was not saved (language value matches global), return the global setting
        if (setting == null)
        {
            setting = await dbContext.Settings!
                .Where(s => s.Key == key && s.UserId == null && s.Language == null)
                .FirstOrDefaultAsync();
        }

        var settingDto = mapper.Map<SettingDetailsDto>(setting);
        return Ok(settingDto);
    }

    /// <summary>
    /// Delete a system-level setting (Admin only).
    /// </summary>
    /// <param name="key">Setting key.</param>
    /// <param name="language">Optional language code for language-specific override.</param>
    /// <returns>No content if successful.</returns>
    [HttpDelete("system/{key}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> DeleteSystemSetting(string key, [FromQuery] string? language = null)
    {
        var setting = await dbContext.Settings!
            .Where(s => s.Key == key && s.UserId == null && s.Language == language)
            .FirstOrDefaultAsync();

        if (setting == null)
        {
            return NotFound($"System setting with key '{key}' not found.");
        }

        await settingService.DeleteSystemSettingAsync(key, language);
        return NoContent();
    }

    /// <summary>
    /// Get all effective settings for the current user (user-level settings override system-level).
    /// Optionally pass a language code for language-specific override resolution with fuzzy matching.
    /// </summary>
    /// <param name="language">Optional language code for fuzzy language matching.</param>
    /// <returns>Dictionary of effective settings for the current user.</returns>
    [HttpGet("user")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Dictionary<string, SettingValueDto>>> GetUserSettings([FromQuery] string? language = null)
    {
        var user = await UserHelper.GetCurrentUserOrThrowAsync(userManager, User);

        var effectiveSettings = await settingService.GetEffectiveUserSettingEntitiesAsync(user.Id, language);

        var result = effectiveSettings.ToDictionary(
            s => s.Key,
            s => new SettingValueDto
            {
                Key = s.Key,
                Value = s.Value,
                UserId = s.UserId,
                Required = s.Required,
                Type = s.Type,
                Description = s.Description,
            });

        return Ok(result);
    }

    /// <summary>
    /// Get a specific setting value for the current user (with fallback to system-level).
    /// Optionally pass a language code for language-specific override resolution with fuzzy matching.
    /// </summary>
    /// <param name="key">Setting key.</param>
    /// <param name="language">Optional language code for fuzzy language matching.</param>
    /// <returns>Setting value.</returns>
    [HttpGet("user/{key}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SettingValueDto>> GetUserSetting(string key, [FromQuery] string? language = null)
    {
        var user = await UserHelper.GetCurrentUserOrThrowAsync(userManager, User);

        var result = await settingService.FindEffectiveUserSettingAsync(key, user.Id, language);

        if (result == null)
        {
            return NotFound($"Setting with key '{key}' not found.");
        }

        var dto = new SettingValueDto
        {
            Key = result.Key,
            Value = result.Value,
            UserId = result.UserId,
            Required = result.Required,
            Type = result.Type,
            Description = result.Description,
        };

        return Ok(dto);
    }

    /// <summary>
    /// Set a user-level setting for the current user.
    /// </summary>
    /// <param name="key">Setting key.</param>
    /// <param name="value">Setting value.</param>
    /// <returns>Updated setting details.</returns>
    [HttpPut("user/{key}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SettingDetailsDto>> SetUserSetting(
        string key,
        [FromQuery] string? value)
    {
        var user = await UserHelper.GetCurrentUserOrThrowAsync(userManager, User);

        await settingService.SetUserSettingAsync(key, value, user.Id);

        var setting = await dbContext.Settings!
            .Where(s => s.Key == key && s.UserId == user.Id)
            .FirstOrDefaultAsync();

        // If setting was not saved (value matches system-level), return the system setting
        if (setting == null)
        {
            setting = await dbContext.Settings!
                .Where(s => s.Key == key && s.UserId == null && s.Language == null)
                .FirstOrDefaultAsync();
        }

        var settingDto = mapper.Map<SettingDetailsDto>(setting);
        return Ok(settingDto);
    }

    /// <summary>
    /// Delete a user-level setting for the current user.
    /// </summary>
    /// <param name="key">Setting key.</param>
    /// <returns>No content if successful.</returns>
    [HttpDelete("user/{key}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> DeleteUserSetting(string key)
    {
        var user = await UserHelper.GetCurrentUserOrThrowAsync(userManager, User);

        var setting = await dbContext.Settings!
            .Where(s => s.Key == key && s.UserId == user.Id)
            .FirstOrDefaultAsync();

        if (setting == null)
        {
            return NotFound($"User setting with key '{key}' not found.");
        }

        await settingService.DeleteUserSettingAsync(key, user.Id);
        return NoContent();
    }

    /// <summary>
    /// Get all user-level settings for the current user (no fallback to system settings).
    /// </summary>
    /// <returns>List of user-level settings.</returns>
    [HttpGet("user/overrides")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<SettingDetailsDto>>> GetUserOverrides()
    {
        var user = await UserHelper.GetCurrentUserOrThrowAsync(userManager, User);

        var settings = await dbContext.Settings!
            .Where(s => s.UserId == user.Id)
            .ToListAsync();

        var settingDtos = mapper.Map<List<SettingDetailsDto>>(settings);

        return Ok(settingDtos);
    }

    /// <inheritdoc/>
    [HttpGet("sync")]
    [ProducesResponseType(typeof(SyncResponseDto<SettingDetailsDto, int>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override Task<IActionResult> Sync([FromQuery] string? syncToken = null, [FromQuery] string? query = null)
    {
        return base.Sync(syncToken, query);
    }

    /// <inheritdoc/>
    protected override async Task OnAfterImportAsync(List<Setting> importedEntities, List<SettingImportDto> importRecords)
    {
        var allImported = await dbContext.Settings!
            .Where(s => importRecords.Select(r => r.Key).Contains(s.Key))
            .ToListAsync();

        var toRemove = new List<Setting>();

        foreach (var setting in allImported)
        {
            // Enrich with plugin metadata only if not already set (i.e. first save)
            if (setting.Type == null)
            {
                var pluginDef = settingsEnrichmentService.GetSettingDefinitions()
                    .FirstOrDefault(d => d.Key == setting.Key);

                if (pluginDef != null)
                {
                    setting.Required = pluginDef.Required;
                    setting.Type = pluginDef.Type;
                    setting.Description = pluginDef.Description;
                }
            }

            // Mark language/user-specific settings for removal if they match the global value
            if (!string.IsNullOrEmpty(setting.Language) || !string.IsNullOrEmpty(setting.UserId))
            {
                var globalSetting = await dbContext.Settings!
                    .Where(s => s.Key == setting.Key && s.UserId == null && s.Language == null)
                    .FirstOrDefaultAsync();

                if (globalSetting != null && globalSetting.Value == setting.Value)
                {
                    toRemove.Add(setting);
                }
            }
        }

        if (toRemove.Count > 0)
        {
            dbContext.Settings!.RemoveRange(toRemove);
        }

        await dbContext.SaveChangesAsync();
    }
}
