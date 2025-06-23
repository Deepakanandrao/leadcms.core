// <copyright file="SettingService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Entities;
using LeadCMS.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Services;

public class SettingService : ISettingService
{
    private readonly PgDbContext dbContext;

    public SettingService(PgDbContext dbContext)
    {
        this.dbContext = dbContext;
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

    public async Task SetUserSettingAsync(string key, string value, string userId)
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

    public async Task SetSystemSettingAsync(string key, string value)
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

    public async Task<Dictionary<string, string>> GetEffectiveUserSettingsAsync(string userId)
    {
        // Get all system settings
        var systemSettings = await dbContext.Settings!
            .Where(s => s.UserId == null)
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        // Get all user settings
        var userSettings = await dbContext.Settings!
            .Where(s => s.UserId == userId)
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        // Merge them, with user settings overriding system settings
        var effectiveSettings = new Dictionary<string, string>(systemSettings);
        foreach (var userSetting in userSettings)
        {
            effectiveSettings[userSetting.Key] = userSetting.Value;
        }

        return effectiveSettings;
    }

    public async Task<Dictionary<string, string>> GetSystemSettingsAsync()
    {
        return await dbContext.Settings!
            .Where(s => s.UserId == null)
            .ToDictionaryAsync(s => s.Key, s => s.Value);
    }
}
