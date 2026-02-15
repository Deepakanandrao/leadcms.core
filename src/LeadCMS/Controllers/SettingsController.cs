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
    /// Get all system-level settings enriched with default values from appsettings (Admin only).
    /// Database settings take precedence over appsettings defaults.
    /// </summary>
    /// <returns>List of system-level settings enriched with defaults.</returns>
    [HttpGet("system")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<SettingDetailsDto>>> GetSystemSettings()
    {
        // Get database settings
        var dbSettings = await dbContext.Settings!
            .Where(s => s.UserId == null)
            .ToListAsync();

        // Create a dictionary for easy lookup and manipulation
        var settingsDict = dbSettings.ToDictionary(s => s.Key, s => s.Value);

        // Enrich with default values using the new enrichment service
        // This handles both missing keys and null values in the database
        await settingsEnrichmentService.EnrichWithAllKnownSettingsAsync(settingsDict);

        // Convert back to Setting entities for consistent response format
        var enrichedSettings = settingsDict.Select(kvp =>
        {
            var existingDbSetting = dbSettings.FirstOrDefault(s => s.Key == kvp.Key);
            return new Setting
            {
                Id = existingDbSetting?.Id ?? 0,
                Key = kvp.Key,
                Value = kvp.Value, // Use the enriched value from the dictionary
                UserId = null,
                CreatedAt = existingDbSetting?.CreatedAt ?? DateTime.UtcNow,
                CreatedById = existingDbSetting?.CreatedById,
                CreatedByIp = existingDbSetting?.CreatedByIp,
                CreatedByUserAgent = existingDbSetting?.CreatedByUserAgent,
                UpdatedAt = existingDbSetting?.UpdatedAt,
                UpdatedById = existingDbSetting?.UpdatedById,
                UpdatedByIp = existingDbSetting?.UpdatedByIp,
                UpdatedByUserAgent = existingDbSetting?.UpdatedByUserAgent,
                Source = existingDbSetting?.Source,
            };
        }).ToList();

        var settingDtos = mapper.Map<List<SettingDetailsDto>>(enrichedSettings);
        return Ok(settingDtos);
    }

    /// <summary>
    /// Get a specific system-level setting by key (Admin only).
    /// </summary>
    /// <param name="key">Setting key.</param>
    /// <returns>System-level setting details.</returns>
    [HttpGet("system/{key}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SettingDetailsDto>> GetSystemSetting(string key)
    {
        var setting = await dbContext.Settings!
            .Where(s => s.Key == key && s.UserId == null)
            .FirstOrDefaultAsync();

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
        [FromQuery] string? value)
    {
        await settingService.SetSystemSettingAsync(key, value);

        var setting = await dbContext.Settings!
            .Where(s => s.Key == key && s.UserId == null)
            .FirstOrDefaultAsync();

        var settingDto = mapper.Map<SettingDetailsDto>(setting);
        return Ok(settingDto);
    }

    /// <summary>
    /// Delete a system-level setting (Admin only).
    /// </summary>
    /// <param name="key">Setting key.</param>
    /// <returns>No content if successful.</returns>
    [HttpDelete("system/{key}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> DeleteSystemSetting(string key)
    {
        var setting = await dbContext.Settings!
            .Where(s => s.Key == key && s.UserId == null)
            .FirstOrDefaultAsync();

        if (setting == null)
        {
            return NotFound($"System setting with key '{key}' not found.");
        }

        await settingService.DeleteSystemSettingAsync(key);
        return NoContent();
    }

    /// <summary>
    /// Get all effective settings for the current user (user-level settings override system-level).
    /// </summary>
    /// <returns>Dictionary of effective settings for the current user.</returns>
    [HttpGet("user")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Dictionary<string, SettingValueDto>>> GetUserSettings()
    {
        var user = await UserHelper.GetCurrentUserOrThrowAsync(userManager, User);

        var systemSettings = await dbContext.Settings!
            .Where(s => s.UserId == null)
            .ToListAsync();

        var userSettings = await dbContext.Settings!
            .Where(s => s.UserId == user.Id)
            .ToListAsync();

        var result = new Dictionary<string, SettingValueDto>();

        // Add system settings first
        foreach (var setting in systemSettings)
        {
            result[setting.Key] = new SettingValueDto
            {
                Key = setting.Key,
                Value = setting.Value,
                IsUserLevel = false,
            };
        }

        // Override with user settings
        foreach (var setting in userSettings)
        {
            result[setting.Key] = new SettingValueDto
            {
                Key = setting.Key,
                Value = setting.Value,
                IsUserLevel = true,
            };
        }

        return Ok(result);
    }

    /// <summary>
    /// Get a specific setting value for the current user (with fallback to system-level).
    /// </summary>
    /// <param name="key">Setting key.</param>
    /// <returns>Setting value.</returns>
    [HttpGet("user/{key}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SettingValueDto>> GetUserSetting(string key)
    {
        var user = await UserHelper.GetCurrentUserOrThrowAsync(userManager, User);

        // First try user-level setting
        var userSetting = await dbContext.Settings!
            .Where(s => s.Key == key && s.UserId == user.Id)
            .FirstOrDefaultAsync();

        if (userSetting != null)
        {
            return Ok(new SettingValueDto
            {
                Key = userSetting.Key,
                Value = userSetting.Value,
                IsUserLevel = true,
            });
        }

        // Fall back to system-level setting
        var systemSetting = await dbContext.Settings!
            .Where(s => s.Key == key && s.UserId == null)
            .FirstOrDefaultAsync();

        if (systemSetting != null)
        {
            return Ok(new SettingValueDto
            {
                Key = systemSetting.Key,
                Value = systemSetting.Value,
                IsUserLevel = false,
            });
        }

        return NotFound($"Setting with key '{key}' not found.");
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
}
