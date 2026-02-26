// <copyright file="SettingsMetadataAndSkipTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Tests;

/// <summary>
/// Tests for three settings refinements:
/// 1. Language/user-specific settings are only persisted when the value differs from the global setting.
/// 2. Metadata fields (Required, Type, Description) are read-only — clients cannot set them via APIs.
/// 3. Plugin-registered settings always return metadata from plugin definitions and are enriched on save.
/// </summary>
public class SettingsMetadataAndSkipTests : BaseTestAutoLogin
{
    public SettingsMetadataAndSkipTests()
        : base()
    {
        TrackEntityType<Setting>();
    }

    // =============================
    // Requirement 1: Skip save if value matches global
    // =============================

    [Fact]
    public async Task SetSystemSetting_LanguageOverride_SkippedWhenValueMatchesGlobal()
    {
        // Arrange - create a global system setting
        var globalUrl = $"/api/settings/system/{Uri.EscapeDataString("Test.Skip.Lang")}?value=hello";
        await Request(HttpMethod.Put, globalUrl, null);

        // Act - try to set a language-specific override with the same value
        var langUrl = $"/api/settings/system/{Uri.EscapeDataString("Test.Skip.Lang")}?value=hello&language=en";
        await Request(HttpMethod.Put, langUrl, null);

        // Assert - no language-specific row should exist
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();

        var langSetting = await dbContext.Settings!
            .FirstOrDefaultAsync(s => s.Key == "Test.Skip.Lang" && s.Language == "en" && s.UserId == null);
        Assert.Null(langSetting);

        // The global row should still exist
        var globalSetting = await dbContext.Settings!
            .FirstOrDefaultAsync(s => s.Key == "Test.Skip.Lang" && s.Language == null && s.UserId == null);
        Assert.NotNull(globalSetting);
        Assert.Equal("hello", globalSetting!.Value);
    }

    [Fact]
    public async Task SetSystemSetting_LanguageOverride_SavedWhenValueDiffersFromGlobal()
    {
        // Arrange
        var globalUrl = $"/api/settings/system/{Uri.EscapeDataString("Test.Skip.LangDiff")}?value=hello";
        await Request(HttpMethod.Put, globalUrl, null);

        // Act - set a language override with a DIFFERENT value
        var langUrl = $"/api/settings/system/{Uri.EscapeDataString("Test.Skip.LangDiff")}?value=bonjour&language=fr";
        await Request(HttpMethod.Put, langUrl, null);

        // Assert - language-specific row should exist
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();

        var langSetting = await dbContext.Settings!
            .FirstOrDefaultAsync(s => s.Key == "Test.Skip.LangDiff" && s.Language == "fr" && s.UserId == null);
        Assert.NotNull(langSetting);
        Assert.Equal("bonjour", langSetting!.Value);
    }

    [Fact]
    public async Task SetSystemSetting_LanguageOverride_DeletesExistingWhenValueMatchesGlobal()
    {
        // Arrange - create global + language override with different values
        var globalUrl = $"/api/settings/system/{Uri.EscapeDataString("Test.Skip.DelLang")}?value=hello";
        await Request(HttpMethod.Put, globalUrl, null);

        var langUrl = $"/api/settings/system/{Uri.EscapeDataString("Test.Skip.DelLang")}?value=hola&language=es";
        await Request(HttpMethod.Put, langUrl, null);

        // Verify the language override exists
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();

        var before = await dbContext.Settings!
            .FirstOrDefaultAsync(s => s.Key == "Test.Skip.DelLang" && s.Language == "es" && s.UserId == null);
        Assert.NotNull(before);

        // Act - update the language override to match the global value
        var updateUrl = $"/api/settings/system/{Uri.EscapeDataString("Test.Skip.DelLang")}?value=hello&language=es";
        await Request(HttpMethod.Put, updateUrl, null);

        // Assert - the language override row should have been deleted
        var dbContext2 = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();
        var after = await dbContext2.Settings!
            .FirstOrDefaultAsync(s => s.Key == "Test.Skip.DelLang" && s.Language == "es" && s.UserId == null);
        Assert.Null(after);
    }

    [Fact]
    public async Task Post_LanguageSpecificSetting_SkippedWhenValueMatchesGlobal()
    {
        // Arrange - create a global setting via Post
        var globalSetting = new SettingCreateDto
        {
            Key = "Test.Skip.PostLang",
            Value = "same-value",
            UserId = null,
        };
        await PostTest<SettingDetailsDto>("/api/settings", globalSetting, HttpStatusCode.Created);

        // Act - post a language-specific setting with the same value
        var langSetting = new SettingCreateDto
        {
            Key = "Test.Skip.PostLang",
            Value = "same-value",
            UserId = null,
            Language = "de",
        };
        var result = await PostTest<SettingDetailsDto>("/api/settings", langSetting, HttpStatusCode.Created);

        // Assert - no language-specific row should exist in DB
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();

        var langDbSetting = await dbContext.Settings!
            .FirstOrDefaultAsync(s => s.Key == "Test.Skip.PostLang" && s.Language == "de" && s.UserId == null);
        Assert.Null(langDbSetting);

        // The response should still have the key and value (from the global setting)
        Assert.NotNull(result);
        Assert.Equal("Test.Skip.PostLang", result!.Key);
        Assert.Equal("same-value", result.Value);
    }

    [Fact]
    public async Task SetUserSetting_SkippedWhenValueMatchesSystemSetting()
    {
        // Arrange - create a system-level setting
        var systemUrl = $"/api/settings/system/{Uri.EscapeDataString("Test.Skip.User")}?value=system-value";
        await Request(HttpMethod.Put, systemUrl, null);

        // Act - try to set a user-level setting with the same value
        var userUrl = $"/api/settings/user/{Uri.EscapeDataString("Test.Skip.User")}?value=system-value";
        await Request(HttpMethod.Put, userUrl, null);

        // Assert - no user-specific row should exist
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();

        var userSettings = await dbContext.Settings!
            .Where(s => s.Key == "Test.Skip.User" && s.UserId != null)
            .ToListAsync();
        Assert.Empty(userSettings);
    }

    [Fact]
    public async Task SetUserSetting_SavedWhenValueDiffersFromSystemSetting()
    {
        // Arrange
        var systemUrl = $"/api/settings/system/{Uri.EscapeDataString("Test.Skip.UserDiff")}?value=system-value";
        await Request(HttpMethod.Put, systemUrl, null);

        // Act - set a user-level setting with a different value
        var userUrl = $"/api/settings/user/{Uri.EscapeDataString("Test.Skip.UserDiff")}?value=user-custom";
        await Request(HttpMethod.Put, userUrl, null);

        // Assert - user-specific row should exist
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();

        var userSettings = await dbContext.Settings!
            .Where(s => s.Key == "Test.Skip.UserDiff" && s.UserId != null)
            .ToListAsync();
        Assert.Single(userSettings);
        Assert.Equal("user-custom", userSettings[0].Value);
    }

    [Fact]
    public async Task SetUserSetting_DeletesExistingWhenValueMatchesSystemSetting()
    {
        // Arrange - create system setting and user override
        var systemUrl = $"/api/settings/system/{Uri.EscapeDataString("Test.Skip.UserDel")}?value=system-value";
        await Request(HttpMethod.Put, systemUrl, null);

        var userUrl = $"/api/settings/user/{Uri.EscapeDataString("Test.Skip.UserDel")}?value=different";
        await Request(HttpMethod.Put, userUrl, null);

        // Verify user setting exists
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();
        var before = await dbContext.Settings!
            .AnyAsync(s => s.Key == "Test.Skip.UserDel" && s.UserId != null);
        Assert.True(before);

        // Act - update user setting to match system value
        var updateUrl = $"/api/settings/user/{Uri.EscapeDataString("Test.Skip.UserDel")}?value=system-value";
        await Request(HttpMethod.Put, updateUrl, null);

        // Assert - user override should be deleted
        var after = await dbContext.Settings!
            .AnyAsync(s => s.Key == "Test.Skip.UserDel" && s.UserId != null);
        Assert.False(after);
    }

    // =============================
    // Requirement 2: Metadata is read-only
    // =============================

    [Fact]
    public async Task Post_MetadataFieldsAreIgnoredFromClientInput()
    {
        // Arrange - create a setting for a NON-plugin key
        // The SettingCreateDto no longer has Required/Type/Description properties,
        // so we send them as extra JSON properties which should be ignored.
        var payload = new
        {
            Key = "Test.Readonly.Meta",
            Value = "some-value",
            Required = true,
            Type = "bool",
            Description = "Should be ignored",
        };

        // Act
        var response = await Request(HttpMethod.Post, "/api/settings", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert - metadata should NOT be persisted for non-plugin settings
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();
        var setting = await dbContext.Settings!
            .FirstOrDefaultAsync(s => s.Key == "Test.Readonly.Meta");

        Assert.NotNull(setting);
        Assert.False(setting!.Required); // Not a plugin setting, should be default
        Assert.Null(setting.Type);
        Assert.Null(setting.Description);
    }

    // =============================
    // Requirement 3: Plugin metadata enrichment
    // =============================

    [Fact]
    public async Task Post_PluginSetting_EnrichesEntityWithPluginMetadataOnSave()
    {
        // Arrange - save a plugin-registered setting
        var settingDto = new SettingCreateDto
        {
            Key = "LeadCapture.Telegram.BotId",
            Value = "my-bot-123",
            UserId = null,
        };

        // Act
        await PostTest<SettingDetailsDto>("/api/settings", settingDto, HttpStatusCode.Created);

        // Assert - the entity in DB should have plugin metadata populated
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();
        var setting = await dbContext.Settings!
            .FirstOrDefaultAsync(s => s.Key == "LeadCapture.Telegram.BotId" && s.UserId == null);

        Assert.NotNull(setting);
        Assert.True(setting!.Required);
        Assert.Equal("text", setting.Type);
        Assert.False(string.IsNullOrEmpty(setting.Description));
    }

    [Fact]
    public async Task GetSystemSetting_Individual_ReturnsPluginMetadata()
    {
        // Arrange - save a plugin-registered setting
        var settingDto = new SettingCreateDto
        {
            Key = "LeadCapture.Email.Enabled",
            Value = "true",
            UserId = null,
        };
        await PostTest<SettingDetailsDto>("/api/settings", settingDto, HttpStatusCode.Created);

        // Act - fetch individual system setting
        var result = await GetTest<SettingDetailsDto>(
            $"/api/settings/system/{Uri.EscapeDataString("LeadCapture.Email.Enabled")}",
            HttpStatusCode.OK);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("bool", result!.Type);
        Assert.False(string.IsNullOrEmpty(result.Description));
    }

    [Fact]
    public async Task GetSystemSetting_Individual_ReturnsPluginDefinitionWhenNotInDb()
    {
        // Act - fetch a plugin-registered setting that has NOT been saved in DB
        var result = await GetTest<SettingDetailsDto>(
            $"/api/settings/system/{Uri.EscapeDataString("LeadCapture.Slack.WebhookUrl")}",
            HttpStatusCode.OK);

        // Assert - should return plugin definition metadata and default value
        Assert.NotNull(result);
        Assert.Equal("LeadCapture.Slack.WebhookUrl", result!.Key);
        Assert.Equal(string.Empty, result.Value);
        Assert.True(result.Required);
        Assert.Equal("text", result.Type);
        Assert.False(string.IsNullOrEmpty(result.Description));
    }

    [Fact]
    public async Task GetSystemSetting_Individual_Returns404ForUnknownNonPluginKey()
    {
        // Act & Assert - non-existent, non-plugin setting should return 404
        await GetTest("/api/settings/system/NonExistent.Key.Here", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PutSystemSetting_EnrichesResponseWithPluginMetadata()
    {
        // Act - set a plugin setting via PUT
        var url = $"/api/settings/system/{Uri.EscapeDataString("LeadCapture.Slack.Enabled")}?value=true";
        var response = await Request(HttpMethod.Put, url, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = Helpers.JsonHelper.Deserialize<SettingDetailsDto>(content);

        // Assert - response should have plugin metadata
        Assert.NotNull(result);
        Assert.Equal("bool", result!.Type);
        Assert.False(string.IsNullOrEmpty(result.Description));
    }

    [Fact]
    public async Task GetUserSettings_EnrichesResponseWithPluginMetadata()
    {
        // Arrange - save a plugin setting as system-level
        var settingDto = new SettingCreateDto
        {
            Key = "LeadCapture.Email.Enabled",
            Value = "true",
            UserId = null,
        };
        await PostTest<SettingDetailsDto>("/api/settings", settingDto, HttpStatusCode.Created);

        // Act - get user settings (which include system-level fallbacks)
        var result = await GetTest<Dictionary<string, SettingValueDto>>("/api/settings/user", HttpStatusCode.OK);

        // Assert - plugin metadata should be present on the setting
        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("LeadCapture.Email.Enabled"));
        var emailEnabled = result["LeadCapture.Email.Enabled"];
        Assert.Equal("bool", emailEnabled.Type);
        Assert.False(string.IsNullOrEmpty(emailEnabled.Description));
    }

    [Fact]
    public async Task SetSystemSetting_LanguageOverride_ReturnsGlobalSettingWhenSkipped()
    {
        // Arrange - create a global system setting
        var globalUrl = $"/api/settings/system/{Uri.EscapeDataString("Test.Skip.Response")}?value=global-val";
        await Request(HttpMethod.Put, globalUrl, null);

        // Act - try to set a language override with the same value
        var langUrl = $"/api/settings/system/{Uri.EscapeDataString("Test.Skip.Response")}?value=global-val&language=en";
        var response = await Request(HttpMethod.Put, langUrl, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = Helpers.JsonHelper.Deserialize<SettingDetailsDto>(content);

        // Assert - response should return the global setting
        Assert.NotNull(result);
        Assert.Equal("Test.Skip.Response", result!.Key);
        Assert.Equal("global-val", result.Value);
    }

    [Fact]
    public async Task SetUserSetting_ReturnsSystemSettingWhenSkipped()
    {
        // Arrange - create a system-level setting
        var systemUrl = $"/api/settings/system/{Uri.EscapeDataString("Test.Skip.UserResp")}?value=system-val";
        await Request(HttpMethod.Put, systemUrl, null);

        // Act - try to set a user-level setting with the same value
        var userUrl = $"/api/settings/user/{Uri.EscapeDataString("Test.Skip.UserResp")}?value=system-val";
        var response = await Request(HttpMethod.Put, userUrl, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = Helpers.JsonHelper.Deserialize<SettingDetailsDto>(content);

        // Assert - response should return the system setting
        Assert.NotNull(result);
        Assert.Equal("Test.Skip.UserResp", result!.Key);
        Assert.Equal("system-val", result.Value);
    }

    [Fact]
    public async Task Import_EnrichesSettingsWithPluginMetadata()
    {
        // Arrange - import records that include a plugin-registered setting
        var importRecords = new List<object>
        {
            new
            {
                Key = "LeadCapture.Telegram.ChatId",
                Value = "12345",
            },
        };

        // Act
        var response = await Request(HttpMethod.Post, "/api/settings/import", importRecords);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - the imported setting should have plugin metadata in DB
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();
        var setting = await dbContext.Settings!
            .FirstOrDefaultAsync(s => s.Key == "LeadCapture.Telegram.ChatId");

        Assert.NotNull(setting);
        Assert.True(setting!.Required);
        Assert.Equal("text", setting.Type);
        Assert.False(string.IsNullOrEmpty(setting.Description));
    }

    [Fact]
    public async Task Import_SkipsLanguageOverridesMatchingGlobalValue()
    {
        // Arrange - create a global setting first
        var globalSetting = new SettingCreateDto
        {
            Key = "Test.Import.SkipLang",
            Value = "global-value",
            UserId = null,
        };
        await PostTest<SettingDetailsDto>("/api/settings", globalSetting, HttpStatusCode.Created);

        // Act - import a language override with the same value
        var importRecords = new List<object>
        {
            new
            {
                Key = "Test.Import.SkipLang",
                Value = "global-value",
                Language = "en",
            },
        };

        var response = await Request(HttpMethod.Post, "/api/settings/import", importRecords);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - the language override should have been removed
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();
        var langSetting = await dbContext.Settings!
            .FirstOrDefaultAsync(s => s.Key == "Test.Import.SkipLang" && s.Language == "en");
        Assert.Null(langSetting);
    }

    [Fact]
    public async Task SetSystemSetting_GlobalSetting_AlwaysSavedEvenWithoutExistingGlobal()
    {
        // Act - set a global setting (language=null) when no prior global exists
        var url = $"/api/settings/system/{Uri.EscapeDataString("Test.NoSkip.Global")}?value=brand-new";
        var response = await Request(HttpMethod.Put, url, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - setting should be saved
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();
        var setting = await dbContext.Settings!
            .FirstOrDefaultAsync(s => s.Key == "Test.NoSkip.Global" && s.Language == null && s.UserId == null);
        Assert.NotNull(setting);
        Assert.Equal("brand-new", setting!.Value);
    }

    [Fact]
    public async Task SetSystemSetting_LanguageOverride_SavedWhenNoGlobalExists()
    {
        // Act - set a language-specific setting when NO global setting exists
        var langUrl = $"/api/settings/system/{Uri.EscapeDataString("Test.NoGlobal.Lang")}?value=hola&language=es";
        var response = await Request(HttpMethod.Put, langUrl, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - setting should be saved because there is no global to compare against
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();
        var langSetting = await dbContext.Settings!
            .FirstOrDefaultAsync(s => s.Key == "Test.NoGlobal.Lang" && s.Language == "es" && s.UserId == null);
        Assert.NotNull(langSetting);
        Assert.Equal("hola", langSetting!.Value);
    }
}
