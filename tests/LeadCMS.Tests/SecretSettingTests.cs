// <copyright file="SecretSettingTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Constants;
using LeadCMS.Controllers;
using LeadCMS.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Tests;

public class SecretSettingTests : BaseTestAutoLogin
{
    public SecretSettingTests()
        : base()
    {
        TrackEntityType<Setting>();
    }

    [Fact]
    public async Task GetSystemSettings_MasksSecretSettingValues()
    {
        await SeedSecretSetting("my-secret-api-key-value");

        var settings = await GetTest<List<SettingDetailsDto>>("/api/settings/system", HttpStatusCode.OK);

        Assert.NotNull(settings);

        var apiKeySetting = settings.FirstOrDefault(s => s.Key == SettingKeys.DeploymentWebhooksApiKey);
        Assert.NotNull(apiKeySetting);
        Assert.Equal(SettingListHelper.SecretMask, apiKeySetting.Value);
        Assert.Equal(SettingValueTypes.Secret, apiKeySetting.Type);
    }

    [Fact]
    public async Task GetSystemSetting_MasksSecretSettingValue()
    {
        await SeedSecretSetting("my-secret-api-key-value");

        var setting = await GetTest<SettingDetailsDto>(
            $"/api/settings/system/{SettingKeys.DeploymentWebhooksApiKey}",
            HttpStatusCode.OK);

        Assert.NotNull(setting);
        Assert.Equal(SettingListHelper.SecretMask, setting.Value);
    }

    [Fact]
    public async Task GetSystemSettings_SecretWithNoValue_ReturnsNullNotMask()
    {
        // Don't seed any value — the setting should appear via metadata enrichment with null value
        var settings = await GetTest<List<SettingDetailsDto>>("/api/settings/system", HttpStatusCode.OK);

        Assert.NotNull(settings);

        var apiKeySetting = settings.FirstOrDefault(s => s.Key == SettingKeys.DeploymentWebhooksApiKey);
        Assert.NotNull(apiKeySetting);
        Assert.True(string.IsNullOrEmpty(apiKeySetting.Value));
    }

    [Fact]
    public async Task GetConfig_DoesNotExposeDeploymentApiKey()
    {
        await SeedSecretSetting("my-secret-api-key-value");

        var configDto = await GetTest<ConfigDto>("/api/config", HttpStatusCode.OK);

        Assert.NotNull(configDto);
        Assert.NotNull(configDto.Settings);
        Assert.False(configDto.Settings.ContainsKey(SettingKeys.DeploymentWebhooksApiKey));
    }

    [Fact]
    public async Task PutSystemSetting_WithSecretMask_PreservesOriginalValue()
    {
        await SeedSecretSetting("real-secret-value");

        // Try to update with the mask placeholder via PUT
        var url = $"/api/settings/system/{Uri.EscapeDataString(SettingKeys.DeploymentWebhooksApiKey)}?value={Uri.EscapeDataString(SettingListHelper.SecretMask)}";
        await Request(HttpMethod.Put, url, null);

        // Verify the original value is preserved in the database
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();

        var setting = await dbContext.Settings!
            .FirstOrDefaultAsync(s => s.Key == SettingKeys.DeploymentWebhooksApiKey && s.UserId == null);

        Assert.NotNull(setting);
        Assert.Equal("real-secret-value", setting.Value);
    }

    private async Task SeedSecretSetting(string value)
    {
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();

        dbContext.Settings!.Add(new Setting
        {
            Key = SettingKeys.DeploymentWebhooksApiKey,
            Value = value,
            Type = SettingValueTypes.Secret,
            UserId = null,
            CreatedAt = DateTime.UtcNow,
        });

        await dbContext.SaveChangesAsync();
    }
}
