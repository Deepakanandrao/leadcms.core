// <copyright file="SettingsTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Tests;

public class SettingsTests : BaseTestAutoLogin
{
    public SettingsTests()
        : base()
    {
        TrackEntityType<Setting>();
    }

    [Fact]
    public async Task GetSystemSettings_ReturnsEnrichedWithDefaults()
    {
        // Arrange - No specific setup needed, we want to test enrichment with default values

        // Act
        var settings = await GetTest<List<SettingDetailsDto>>("/api/settings/system", HttpStatusCode.OK);

        // Assert
        Assert.NotNull(settings);
        Assert.NotEmpty(settings);

        // Verify that default settings from appsettings.json are included
        var settingDict = settings.ToDictionary(s => s.Key, s => s.Value);

        // Content validation settings should be present with default values
        Assert.True(settingDict.ContainsKey(SettingKeys.MinTitleLength));
        Assert.Equal("10", settingDict[SettingKeys.MinTitleLength]);

        Assert.True(settingDict.ContainsKey(SettingKeys.MaxTitleLength));
        Assert.Equal("60", settingDict[SettingKeys.MaxTitleLength]);

        Assert.True(settingDict.ContainsKey(SettingKeys.MinDescriptionLength));
        Assert.Equal("20", settingDict[SettingKeys.MinDescriptionLength]);

        Assert.True(settingDict.ContainsKey(SettingKeys.MaxDescriptionLength));
        Assert.Equal("155", settingDict[SettingKeys.MaxDescriptionLength]);

        // Identity settings should be present with default values
        Assert.True(settingDict.ContainsKey(SettingKeys.RequireDigit));
        Assert.Equal("true", settingDict[SettingKeys.RequireDigit]);

        Assert.True(settingDict.ContainsKey(SettingKeys.RequireUppercase));
        Assert.Equal("true", settingDict[SettingKeys.RequireUppercase]);

        Assert.True(settingDict.ContainsKey(SettingKeys.RequireLowercase));
        Assert.Equal("true", settingDict[SettingKeys.RequireLowercase]);

        Assert.True(settingDict.ContainsKey(SettingKeys.RequireNonAlphanumeric));
        Assert.Equal("true", settingDict[SettingKeys.RequireNonAlphanumeric]);

        Assert.True(settingDict.ContainsKey(SettingKeys.RequiredLength));
        Assert.Equal("6", settingDict[SettingKeys.RequiredLength]);

        Assert.True(settingDict.ContainsKey(SettingKeys.RequiredUniqueChars));
        Assert.Equal("1", settingDict[SettingKeys.RequiredUniqueChars]);
    }

    [Fact]
    public async Task GetSystemSettings_DatabaseOverridesDefaults()
    {
        // Arrange - Create a system setting that overrides a default value
        var testSetting = new SettingCreateDto
        {
            Key = SettingKeys.MinTitleLength,
            Value = "15", // Different from default value of 10
            UserId = null, // System setting
        };

        await PostTest("/api/settings", testSetting);

        // Act
        var settings = await GetTest<List<SettingDetailsDto>>("/api/settings/system", HttpStatusCode.OK);

        // Assert
        Assert.NotNull(settings);
        Assert.NotEmpty(settings);

        var minTitleLengthSetting = settings.FirstOrDefault(s => s.Key == SettingKeys.MinTitleLength);
        Assert.NotNull(minTitleLengthSetting);
        Assert.Equal("15", minTitleLengthSetting.Value); // Should be database value, not default

        // Other defaults should still be present
        var settingDict = settings.ToDictionary(s => s.Key, s => s.Value);
        Assert.True(settingDict.ContainsKey(SettingKeys.MaxTitleLength));
        Assert.Equal("60", settingDict[SettingKeys.MaxTitleLength]); // Still default
    }

    [Fact]
    public async Task GetSystemSettings_NullDatabaseValueUsesDefault()
    {
        // Arrange - Create a system setting with null value
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();

        // Insert a setting with null value directly into database
        var nullSetting = new Setting
        {
            Key = SettingKeys.MaxTitleLength,
            Value = null, // Null value in database
            UserId = null, // System setting
            CreatedAt = DateTime.UtcNow,
        };

        dbContext.Settings!.Add(nullSetting);
        await dbContext.SaveChangesAsync();

        // Act
        var settings = await GetTest<List<SettingDetailsDto>>("/api/settings/system", HttpStatusCode.OK);

        // Assert
        Assert.NotNull(settings);
        Assert.NotEmpty(settings);

        var settingDict = settings.ToDictionary(s => s.Key, s => s.Value);

        // Setting with null value in database should be enriched with default
        Assert.True(settingDict.ContainsKey(SettingKeys.MaxTitleLength));
        Assert.Equal("60", settingDict[SettingKeys.MaxTitleLength]); // Should use default value since DB has null

        // Other settings should also have defaults
        Assert.True(settingDict.ContainsKey(SettingKeys.MinTitleLength));
        Assert.Equal("10", settingDict[SettingKeys.MinTitleLength]);
    }

    [Fact]
    public async Task GetSystemSettings_RequiresAdminRole()
    {
        // Arrange - Logout to test without authentication
        Logout();

        // Act & Assert
        await GetTest("/api/settings/system", HttpStatusCode.Unauthorized);
    }
}