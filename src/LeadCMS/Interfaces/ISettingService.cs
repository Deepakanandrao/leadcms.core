// <copyright file="ISettingService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Interfaces;

public interface ISettingService
{
    Task<string?> GetUserSettingAsync(string key, string userId);

    Task<string?> GetSystemSettingAsync(string key);

    Task SetUserSettingAsync(string key, string? value, string userId);

    Task SetSystemSettingAsync(string key, string? value);

    Task DeleteUserSettingAsync(string key, string userId);

    Task DeleteSystemSettingAsync(string key);

    Task<Dictionary<string, string?>> GetEffectiveUserSettingsAsync(string userId);

    Task<Dictionary<string, string?>> GetSystemSettingsAsync();

    Task<Dictionary<string, string?>> GetSettingsByKeysAsync(IEnumerable<string> keys, string? userId = null);
}
