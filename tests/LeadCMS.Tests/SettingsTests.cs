// <copyright file="SettingsTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Constants;
using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public async Task Post_SystemSetting_TwiceWithSameKey_DoesNotCreateDuplicate()
    {
        // Arrange
        var settingDto = new SettingCreateDto
        {
            Key = "Test.DuplicateCheck.System",
            Value = "first-value",
            UserId = null,
        };

        // Act - create the same system setting twice
        await PostTest<SettingDetailsDto>("/api/settings", settingDto, HttpStatusCode.Created);

        settingDto.Value = "second-value";
        await PostTest<SettingDetailsDto>("/api/settings", settingDto, HttpStatusCode.Created);

        // Assert - only one row should exist for this key with null UserId
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();

        var count = await dbContext.Settings!
            .CountAsync(s => s.Key == "Test.DuplicateCheck.System" && s.UserId == null);

        Assert.Equal(1, count);

        // The value should be the latest
        var setting = await dbContext.Settings!
            .FirstOrDefaultAsync(s => s.Key == "Test.DuplicateCheck.System" && s.UserId == null);
        Assert.Equal("second-value", setting!.Value);
    }

    [Fact]
    public async Task Post_UserSetting_TwiceWithSameKeyAndUserId_DoesNotCreateDuplicate()
    {
        // Arrange
        var settingDto = new SettingCreateDto
        {
            Key = "Test.DuplicateCheck.User",
            Value = "first-value",
            UserId = "test-user-123",
        };

        // Act - create the same user setting twice
        await PostTest<SettingDetailsDto>("/api/settings", settingDto, HttpStatusCode.Created);

        settingDto.Value = "second-value";
        await PostTest<SettingDetailsDto>("/api/settings", settingDto, HttpStatusCode.Created);

        // Assert - only one row should exist for this key + userId pair
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();

        var count = await dbContext.Settings!
            .CountAsync(s => s.Key == "Test.DuplicateCheck.User" && s.UserId == "test-user-123");

        Assert.Equal(1, count);

        // The value should be the latest
        var setting = await dbContext.Settings!
            .FirstOrDefaultAsync(s => s.Key == "Test.DuplicateCheck.User" && s.UserId == "test-user-123");
        Assert.Equal("second-value", setting!.Value);
    }

    [Fact]
    public async Task Post_SameKeyDifferentUserId_CreatesSeparateSettings()
    {
        // Arrange - same key but different user IDs (including null for system)
        var systemSetting = new SettingCreateDto
        {
            Key = "Test.DuplicateCheck.Mixed",
            Value = "system-value",
            UserId = null,
        };

        var userSetting = new SettingCreateDto
        {
            Key = "Test.DuplicateCheck.Mixed",
            Value = "user-value",
            UserId = "test-user-456",
        };

        // Act
        await PostTest<SettingDetailsDto>("/api/settings", systemSetting, HttpStatusCode.Created);
        await PostTest<SettingDetailsDto>("/api/settings", userSetting, HttpStatusCode.Created);

        // Assert - two separate rows should exist (different UserId)
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();

        var count = await dbContext.Settings!
            .CountAsync(s => s.Key == "Test.DuplicateCheck.Mixed");

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task PutSystemSetting_TwiceWithSameKey_DoesNotCreateDuplicate()
    {
        // Arrange & Act - set the same system setting twice via PUT
        var url1 = $"/api/settings/system/{Uri.EscapeDataString("Test.DuplicateCheck.Put")}?value=first";
        var url2 = $"/api/settings/system/{Uri.EscapeDataString("Test.DuplicateCheck.Put")}?value=second";

        await Request(HttpMethod.Put, url1, null);
        await Request(HttpMethod.Put, url2, null);

        // Assert
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();

        var count = await dbContext.Settings!
            .CountAsync(s => s.Key == "Test.DuplicateCheck.Put" && s.UserId == null);

        Assert.Equal(1, count);

        var setting = await dbContext.Settings!
            .FirstOrDefaultAsync(s => s.Key == "Test.DuplicateCheck.Put" && s.UserId == null);
        Assert.Equal("second", setting!.Value);
    }

    [Fact]
    public async Task Import_SystemSettings_DoesNotCreateDuplicates()
    {
        // Arrange - first create a system setting
        var settingDto = new SettingCreateDto
        {
            Key = "Test.DuplicateCheck.Import",
            Value = "original-value",
            UserId = null,
        };

        await PostTest<SettingDetailsDto>("/api/settings", settingDto, HttpStatusCode.Created);

        // Act - import settings with the same key and null userId
        var importRecords = new List<SettingImportDto>
        {
            new SettingImportDto
            {
                Key = "Test.DuplicateCheck.Import",
                Value = "imported-value",
                UserId = null,
            },
        };

        var response = await Request(HttpMethod.Post, "/api/settings/import", importRecords);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - only one row should exist
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();

        var count = await dbContext.Settings!
            .CountAsync(s => s.Key == "Test.DuplicateCheck.Import" && s.UserId == null);

        Assert.Equal(1, count);
    }
}