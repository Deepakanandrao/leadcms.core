// <copyright file="ConfigTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Constants;
using LeadCMS.Controllers;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Tests;

public class ConfigTests : BaseTest
{
    [Fact]
    public async Task GetConfig_ReturnsContentValidationSettings()
    {
        // Arrange
        // Act
        var configDto = await GetTest<ConfigDto>("/api/config", HttpStatusCode.OK);

        Assert.NotNull(configDto);
        Assert.NotNull(configDto.Settings);
        Assert.Equal("10", configDto.Settings[SettingKeys.MinTitleLength]);
        Assert.Equal("60", configDto.Settings[SettingKeys.MaxTitleLength]);
        Assert.Equal("20", configDto.Settings[SettingKeys.MinDescriptionLength]);
        Assert.Equal("155", configDto.Settings[SettingKeys.MaxDescriptionLength]);
    }

    [Fact]
    public async Task GetConfig_ReturnsIdentitySettings()
    {
        // Arrange
        // Act
        var configDto = await GetTest<ConfigDto>("/api/config", HttpStatusCode.OK);

        Assert.NotNull(configDto);
        Assert.NotNull(configDto.Settings);

        // Verify Identity settings are present and match appsettings.json values
        Assert.Equal("true", configDto.Settings[SettingKeys.RequireDigit]);
        Assert.Equal("true", configDto.Settings[SettingKeys.RequireUppercase]);
        Assert.Equal("true", configDto.Settings[SettingKeys.RequireLowercase]);
        Assert.Equal("true", configDto.Settings[SettingKeys.RequireNonAlphanumeric]);
        Assert.Equal("6", configDto.Settings[SettingKeys.RequiredLength]);
        Assert.Equal("1", configDto.Settings[SettingKeys.RequiredUniqueChars]);

        // Verify excluded settings are NOT present
        Assert.False(configDto.Settings.ContainsKey("Identity.LockoutTime"));
        Assert.False(configDto.Settings.ContainsKey("Identity.MaxFailedAccessAttempts"));
    }

    [Fact]
    public async Task GetConfig_DoesNotReturnEmptyGeneralUrls()
    {
        var configDto = await GetTest<ConfigDto>("/api/config", HttpStatusCode.OK);

        Assert.NotNull(configDto);
        Assert.NotNull(configDto.Settings);
        Assert.False(configDto.Settings.ContainsKey(SettingKeys.GeneralSiteUrl));
        Assert.False(configDto.Settings.ContainsKey(SettingKeys.GeneralUnsubscribeUrl));
        Assert.False(configDto.Settings.ContainsKey(SettingKeys.GeneralPrivacyUrl));
    }

    [Fact]
    public async Task GetConfig_ReturnsGeneralUrlsWhenPresent()
    {
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();

        dbContext.Settings!.Add(new Setting
        {
            Key = SettingKeys.GeneralSiteUrl,
            Value = "https://example.com/",
            UserId = null,
            CreatedAt = DateTime.UtcNow,
        });

        await dbContext.SaveChangesAsync();

        var configDto = await GetTest<ConfigDto>("/api/config", HttpStatusCode.OK);

        Assert.NotNull(configDto);
        Assert.NotNull(configDto.Settings);
        Assert.Equal("https://example.com/", configDto.Settings[SettingKeys.GeneralSiteUrl]);
        Assert.Equal("https://example.com/unsubscribe", configDto.Settings[SettingKeys.GeneralUnsubscribeUrl]);
        Assert.Equal("https://example.com/privacy", configDto.Settings[SettingKeys.GeneralPrivacyUrl]);
    }

    [Fact]
    public async Task GetConfig_ReturnsLastReleaseDateWhenPresent()
    {
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();

        dbContext.Settings!.Add(new Setting
        {
            Key = SettingKeys.GeneralLastReleaseDate,
            Value = "2026-04-01T14:30:00Z",
            UserId = null,
            CreatedAt = DateTime.UtcNow,
        });

        await dbContext.SaveChangesAsync();

        var configDto = await GetTest<ConfigDto>("/api/config", HttpStatusCode.OK);

        Assert.NotNull(configDto);
        Assert.NotNull(configDto.Settings);
        Assert.Equal("2026-04-01T14:30:00Z", configDto.Settings[SettingKeys.GeneralLastReleaseDate]);
    }

    [Fact]
    public async Task GetConfig_DoesNotReturnEmptyLastReleaseDate()
    {
        var configDto = await GetTest<ConfigDto>("/api/config", HttpStatusCode.OK);

        Assert.NotNull(configDto);
        Assert.NotNull(configDto.Settings);
        Assert.False(configDto.Settings.ContainsKey(SettingKeys.GeneralLastReleaseDate));
    }
}