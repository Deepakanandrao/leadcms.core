// <copyright file="LanguageSpecificSettingsTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Tests;

public class LanguageSpecificSettingsTests : BaseTestAutoLogin
{
    public LanguageSpecificSettingsTests()
        : base()
    {
        TrackEntityType<Setting>();
    }

    [Fact]
    public async Task Post_SettingWithLanguage_CreatesLanguageSpecificSetting()
    {
        // Arrange
        var settingDto = new SettingCreateDto
        {
            Key = "Test.Greeting",
            Value = "Hello",
            UserId = null,
            Language = "en",
        };

        // Act
        var result = await PostTest<SettingDetailsDto>("/api/settings", settingDto, HttpStatusCode.Created);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test.Greeting", result.Key);
        Assert.Equal("Hello", result.Value);
        Assert.Equal("en", result.Language);
    }

    [Fact]
    public async Task Post_SameKeyDifferentLanguage_CreatesSeparateSettings()
    {
        // Arrange - create general and language-specific settings with the same key
        var generalSetting = new SettingCreateDto
        {
            Key = "Test.Greeting.Multi",
            Value = "Hello",
            UserId = null,
        };

        var frenchSetting = new SettingCreateDto
        {
            Key = "Test.Greeting.Multi",
            Value = "Bonjour",
            UserId = null,
            Language = "fr",
        };

        // Act
        await PostTest<SettingDetailsDto>("/api/settings", generalSetting, HttpStatusCode.Created);
        await PostTest<SettingDetailsDto>("/api/settings", frenchSetting, HttpStatusCode.Created);

        // Assert - two separate rows should exist
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();

        var count = await dbContext.Settings!
            .CountAsync(s => s.Key == "Test.Greeting.Multi" && s.UserId == null);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Post_SameKeyAndLanguage_DoesNotCreateDuplicate()
    {
        // Arrange
        var settingDto = new SettingCreateDto
        {
            Key = "Test.Dedupe.Lang",
            Value = "first-value",
            UserId = null,
            Language = "de",
        };

        // Act - create the same language-specific setting twice
        await PostTest<SettingDetailsDto>("/api/settings", settingDto, HttpStatusCode.Created);

        settingDto.Value = "second-value";
        await PostTest<SettingDetailsDto>("/api/settings", settingDto, HttpStatusCode.Created);

        // Assert - only one row for this key + language
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();

        var count = await dbContext.Settings!
            .CountAsync(s => s.Key == "Test.Dedupe.Lang" && s.Language == "de" && s.UserId == null);

        Assert.Equal(1, count);

        var setting = await dbContext.Settings!
            .FirstOrDefaultAsync(s => s.Key == "Test.Dedupe.Lang" && s.Language == "de" && s.UserId == null);
        Assert.Equal("second-value", setting!.Value);
    }

    [Fact]
    public async Task GetSystemSettings_WithoutLanguage_ReturnsOnlyGeneralSettings()
    {
        // Arrange - create general and language-specific settings
        var generalSetting = new SettingCreateDto
        {
            Key = "Test.Lang.Filter",
            Value = "general-value",
            UserId = null,
        };

        var langSetting = new SettingCreateDto
        {
            Key = "Test.Lang.Filter",
            Value = "french-value",
            UserId = null,
            Language = "fr",
        };

        await PostTest("/api/settings", generalSetting);
        await PostTest("/api/settings", langSetting);

        // Act - get system settings without language parameter
        var settings = await GetTest<List<SettingDetailsDto>>("/api/settings/system", HttpStatusCode.OK);

        // Assert - should include general setting but not language-specific
        Assert.NotNull(settings);
        var testSettings = settings.Where(s => s.Key == "Test.Lang.Filter").ToList();
        Assert.Single(testSettings);
        Assert.Equal("general-value", testSettings[0].Value);
        Assert.Null(testSettings[0].Language);
    }

    [Fact]
    public async Task GetSystemSettings_WithLanguage_ReturnsGeneralPlusLanguageOverrides()
    {
        // Arrange - create general and language-specific settings
        var generalSetting = new SettingCreateDto
        {
            Key = "Test.Lang.Override",
            Value = "general-value",
            UserId = null,
        };

        var langSetting = new SettingCreateDto
        {
            Key = "Test.Lang.Override",
            Value = "french-value",
            UserId = null,
            Language = "fr",
        };

        // Another setting only defined as general (no French override)
        var generalOnlySetting = new SettingCreateDto
        {
            Key = "Test.Lang.GeneralOnly",
            Value = "only-general",
            UserId = null,
        };

        await PostTest("/api/settings", generalSetting);
        await PostTest("/api/settings", langSetting);
        await PostTest("/api/settings", generalOnlySetting);

        // Act - get system settings with language=fr
        var settings = await GetTest<List<SettingDetailsDto>>("/api/settings/system?language=fr", HttpStatusCode.OK);

        // Assert
        Assert.NotNull(settings);
        var settingDict = settings.ToDictionary(s => s.Key, s => s);

        // Language-specific override should take precedence
        Assert.True(settingDict.ContainsKey("Test.Lang.Override"));
        Assert.Equal("french-value", settingDict["Test.Lang.Override"].Value);
        Assert.Equal("fr", settingDict["Test.Lang.Override"].Language);

        // General-only setting should still be present (no override)
        Assert.True(settingDict.ContainsKey("Test.Lang.GeneralOnly"));
        Assert.Equal("only-general", settingDict["Test.Lang.GeneralOnly"].Value);
    }

    [Fact]
    public async Task PutSystemSetting_WithLanguage_SetsLanguageSpecificValue()
    {
        // Arrange - first create a general setting
        var url = $"/api/settings/system/{Uri.EscapeDataString("Test.Lang.Put")}?value=general";
        await Request(HttpMethod.Put, url, null);

        // Act - set a language-specific override
        var langUrl = $"/api/settings/system/{Uri.EscapeDataString("Test.Lang.Put")}?value=german&language=de";
        await Request(HttpMethod.Put, langUrl, null);

        // Assert - both rows should exist
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();

        var count = await dbContext.Settings!
            .CountAsync(s => s.Key == "Test.Lang.Put" && s.UserId == null);
        Assert.Equal(2, count);

        var generalRow = await dbContext.Settings!
            .FirstOrDefaultAsync(s => s.Key == "Test.Lang.Put" && s.Language == null && s.UserId == null);
        Assert.Equal("general", generalRow!.Value);

        var germanRow = await dbContext.Settings!
            .FirstOrDefaultAsync(s => s.Key == "Test.Lang.Put" && s.Language == "de" && s.UserId == null);
        Assert.Equal("german", germanRow!.Value);
    }

    [Fact]
    public async Task DeleteSystemSetting_WithLanguage_DeletesOnlyLanguageSpecific()
    {
        // Arrange - create general and language-specific settings
        var generalUrl = $"/api/settings/system/{Uri.EscapeDataString("Test.Lang.Del")}?value=general";
        await Request(HttpMethod.Put, generalUrl, null);

        var langUrl = $"/api/settings/system/{Uri.EscapeDataString("Test.Lang.Del")}?value=spanish&language=es";
        await Request(HttpMethod.Put, langUrl, null);

        // Act - delete only the language-specific setting
        var deleteUrl = $"/api/settings/system/{Uri.EscapeDataString("Test.Lang.Del")}?language=es";
        var deleteResponse = await Request(HttpMethod.Delete, deleteUrl, null);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - general setting should remain
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();

        var remaining = await dbContext.Settings!
            .Where(s => s.Key == "Test.Lang.Del" && s.UserId == null)
            .ToListAsync();

        Assert.Single(remaining);
        Assert.Null(remaining[0].Language);
        Assert.Equal("general", remaining[0].Value);
    }

    [Fact]
    public async Task Post_PluginRegisteredSetting_ReturnsMetadataFromPluginDefinition()
    {
        // Arrange - post a setting that matches a plugin-registered key
        var settingDto = new SettingCreateDto
        {
            Key = "LeadCapture.Telegram.BotId",
            Value = "my-bot-id",
            UserId = null,
        };

        // Act
        var result = await PostTest<SettingDetailsDto>("/api/settings", settingDto, HttpStatusCode.Created);

        // Assert - metadata should come from plugin definition, not from client input
        Assert.NotNull(result);
        Assert.True(result.Required);
        Assert.Equal("text", result.Type);
        Assert.False(string.IsNullOrEmpty(result.Description));
    }
}
