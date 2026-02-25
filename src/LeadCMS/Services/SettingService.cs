// <copyright file="SettingService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Entities;
using LeadCMS.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LeadCMS.Services;

public class SettingService : ISettingService
{
    private readonly PgDbContext dbContext;
    private readonly IConfiguration configuration;

    public SettingService(PgDbContext dbContext, IConfiguration configuration)
    {
        this.dbContext = dbContext;
        this.configuration = configuration;
    }

    public async Task<string?> GetUserSettingAsync(string key, string userId)
    {
        // First try to get user-level setting
        var userSetting = await dbContext.Settings!
            .Where(s => s.Key == key && s.UserId == userId)
            .FirstOrDefaultAsync();

        if (userSetting != null)
        {
            return userSetting.Value;
        }

        // Fall back to system-level setting
        return await GetSystemSettingAsync(key);
    }

    public async Task<string?> GetSystemSettingAsync(string key)
    {
        var systemSetting = await dbContext.Settings!
            .Where(s => s.Key == key && s.UserId == null)
            .FirstOrDefaultAsync();

        return systemSetting?.Value;
    }

    public async Task SetUserSettingAsync(string key, string? value, string userId)
    {
        var existingSetting = await dbContext.Settings!
            .Where(s => s.Key == key && s.UserId == userId)
            .FirstOrDefaultAsync();

        if (existingSetting != null)
        {
            existingSetting.Value = value;
            existingSetting.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var newSetting = new Setting
            {
                Key = key,
                Value = value,
                UserId = userId,
            };

            dbContext.Settings!.Add(newSetting);
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task SetSystemSettingAsync(string key, string? value)
    {
        var existingSetting = await dbContext.Settings!
            .Where(s => s.Key == key && s.UserId == null)
            .FirstOrDefaultAsync();

        if (existingSetting != null)
        {
            existingSetting.Value = value;
            existingSetting.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var newSetting = new Setting
            {
                Key = key,
                Value = value,
                UserId = null,
            };

            dbContext.Settings!.Add(newSetting);
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteUserSettingAsync(string key, string userId)
    {
        var setting = await dbContext.Settings!
            .Where(s => s.Key == key && s.UserId == userId)
            .FirstOrDefaultAsync();

        if (setting != null)
        {
            dbContext.Settings!.Remove(setting);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task DeleteSystemSettingAsync(string key)
    {
        var setting = await dbContext.Settings!
            .Where(s => s.Key == key && s.UserId == null)
            .FirstOrDefaultAsync();

        if (setting != null)
        {
            dbContext.Settings!.Remove(setting);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<Dictionary<string, string?>> GetEffectiveUserSettingsAsync(string userId)
    {
        // Get all system settings
        var systemSettings = await dbContext.Settings!
            .Where(s => s.UserId == null)
            .GroupBy(s => s.Key)
            .ToDictionaryAsync(g => g.Key, g => g.First().Value);

        // Get all user settings
        var userSettings = await dbContext.Settings!
            .Where(s => s.UserId == userId)
            .GroupBy(s => s.Key)
            .ToDictionaryAsync(g => g.Key, g => g.First().Value);

        // Merge them, with user settings overriding system settings
        var effectiveSettings = new Dictionary<string, string?>(systemSettings);
        foreach (var userSetting in userSettings)
        {
            effectiveSettings[userSetting.Key] = userSetting.Value;
        }

        return effectiveSettings;
    }

    public async Task<Dictionary<string, string?>> GetSettingsByKeysAsync(IEnumerable<string> keys, string? userId = null)
    {
        var keyList = keys.ToList();
        var result = new Dictionary<string, string?>();

        // Get system-level settings for the keys
        var systemSettings = await dbContext.Settings!
            .Where(s => keyList.Contains(s.Key) && s.UserId == null)
            .GroupBy(s => s.Key)
            .ToDictionaryAsync(g => g.Key, g => g.First().Value);

        foreach (var kvp in systemSettings)
        {
            result[kvp.Key] = kvp.Value;
        }

        // If userId is provided, override with user-level settings for the keys
        if (!string.IsNullOrEmpty(userId))
        {
            var userSettings = await dbContext.Settings!
                .Where(s => keyList.Contains(s.Key) && s.UserId == userId)
                .GroupBy(s => s.Key)
                .ToDictionaryAsync(g => g.Key, g => g.First().Value);

            foreach (var kvp in userSettings)
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        return result;
    }

    public async Task<string?> GetSettingWithFallbackAsync(string key, string configurationPath, string? userId = null)
    {
        // First try to get the setting from database (user-level first, then system-level)
        string? databaseValue = null;
        if (!string.IsNullOrEmpty(userId))
        {
            databaseValue = await GetUserSettingAsync(key, userId);
        }
        else
        {
            databaseValue = await GetSystemSettingAsync(key);
        }

        // If found in database, return it
        if (!string.IsNullOrEmpty(databaseValue))
        {
            return databaseValue;
        }

        // Fall back to configuration
        var configValue = configuration[configurationPath];
        return configValue;
    }

    public async Task<int> GetIntSettingWithFallbackAsync(string key, string configurationPath, int defaultValue = 0, string? userId = null)
    {
        var stringValue = await GetSettingWithFallbackAsync(key, configurationPath, userId);

        if (!string.IsNullOrEmpty(stringValue) && int.TryParse(stringValue, out var intValue) && intValue > 0)
        {
            return intValue;
        }

        return defaultValue;
    }

    public async Task<int> GetIntSettingWithFallbackAsync(string settingKey, int defaultValue = 0, string? userId = null)
    {
        var configurationPath = Constants.ConfigurationPaths.GetConfigurationPath(settingKey);
        return await GetIntSettingWithFallbackAsync(settingKey, configurationPath, defaultValue, userId);
    }

    public async Task<bool> GetBoolSettingWithFallbackAsync(string key, string configurationPath, bool defaultValue = false, string? userId = null)
    {
        var stringValue = await GetSettingWithFallbackAsync(key, configurationPath, userId);

        if (!string.IsNullOrEmpty(stringValue) && bool.TryParse(stringValue, out var boolValue))
        {
            return boolValue;
        }

        return defaultValue;
    }

    public async Task<bool> GetBoolSettingWithFallbackAsync(string settingKey, bool defaultValue = false, string? userId = null)
    {
        var configurationPath = Constants.ConfigurationPaths.GetConfigurationPath(settingKey);
        return await GetBoolSettingWithFallbackAsync(settingKey, configurationPath, defaultValue, userId);
    }
}
