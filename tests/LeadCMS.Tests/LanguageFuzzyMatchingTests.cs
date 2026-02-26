// <copyright file="LanguageFuzzyMatchingTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Tests;

public class LanguageFuzzyMatchingTests : BaseTestAutoLogin
{
    public LanguageFuzzyMatchingTests()
        : base()
    {
        TrackEntityType<Setting>();
    }

    [Fact]
    public async Task GetSystemSettings_WithRuRU_ReturnRuOverride()
    {
        // Arrange — store generic and "ru" language override
        var generalUrl = "/api/settings/system/Test.FuzzyLang.A?value=default";
        await Request(HttpMethod.Put, generalUrl, null);

        var ruUrl = "/api/settings/system/Test.FuzzyLang.A?value=russian&language=ru";
        await Request(HttpMethod.Put, ruUrl, null);

        // Act — request with ru-RU (should match "ru" override)
        var settings = await GetTest<List<SettingDetailsDto>>(
            "/api/settings/system?language=ru-RU", HttpStatusCode.OK);

        // Assert
        Assert.NotNull(settings);
        var setting = settings.First(s => s.Key == "Test.FuzzyLang.A");
        Assert.Equal("russian", setting.Value);
    }

    [Fact]
    public async Task GetSystemSettings_WithRu_ReturnRuRUOverride()
    {
        // Arrange — store generic and "ru-RU" language override
        var generalUrl = "/api/settings/system/Test.FuzzyLang.B?value=default";
        await Request(HttpMethod.Put, generalUrl, null);

        var ruRuUrl = "/api/settings/system/Test.FuzzyLang.B?value=russian-ru&language=ru-RU";
        await Request(HttpMethod.Put, ruRuUrl, null);

        // Act — request with ru (should match "ru-RU" override via fuzzy matching)
        var settings = await GetTest<List<SettingDetailsDto>>(
            "/api/settings/system?language=ru", HttpStatusCode.OK);

        // Assert
        Assert.NotNull(settings);
        var setting = settings.First(s => s.Key == "Test.FuzzyLang.B");
        Assert.Equal("russian-ru", setting.Value);
    }

    [Fact]
    public async Task GetSystemSettings_ExactMatchTakesPrecedenceOverFuzzy()
    {
        // Arrange — store generic, "ru" and "ru-RU" overrides
        var generalUrl = "/api/settings/system/Test.FuzzyLang.C?value=default";
        await Request(HttpMethod.Put, generalUrl, null);

        var ruUrl = "/api/settings/system/Test.FuzzyLang.C?value=russian-short&language=ru";
        await Request(HttpMethod.Put, ruUrl, null);

        var ruRuUrl = "/api/settings/system/Test.FuzzyLang.C?value=russian-full&language=ru-RU";
        await Request(HttpMethod.Put, ruRuUrl, null);

        // Act — request with ru-RU (exact match should win over fuzzy "ru")
        var settings = await GetTest<List<SettingDetailsDto>>(
            "/api/settings/system?language=ru-RU", HttpStatusCode.OK);

        // Assert — exact match "ru-RU" should be used
        Assert.NotNull(settings);
        var setting = settings.First(s => s.Key == "Test.FuzzyLang.C");
        Assert.Equal("russian-full", setting.Value);
    }

    [Fact]
    public async Task GetSystemSettings_NoLanguageMatch_ReturnsGeneric()
    {
        // Arrange — store generic and "fr" override
        var generalUrl = "/api/settings/system/Test.FuzzyLang.D?value=default";
        await Request(HttpMethod.Put, generalUrl, null);

        var frUrl = "/api/settings/system/Test.FuzzyLang.D?value=french&language=fr";
        await Request(HttpMethod.Put, frUrl, null);

        // Act — request with "de" (no match for French)
        var settings = await GetTest<List<SettingDetailsDto>>(
            "/api/settings/system?language=de", HttpStatusCode.OK);

        // Assert — falls back to generic
        Assert.NotNull(settings);
        var setting = settings.First(s => s.Key == "Test.FuzzyLang.D");
        Assert.Equal("default", setting.Value);
    }

    [Fact]
    public async Task GetSystemSettings_CaseInsensitiveMatch()
    {
        // Arrange — store generic and "EN-us" override
        var generalUrl = "/api/settings/system/Test.FuzzyLang.E?value=default";
        await Request(HttpMethod.Put, generalUrl, null);

        var enUrl = "/api/settings/system/Test.FuzzyLang.E?value=english&language=EN-us";
        await Request(HttpMethod.Put, enUrl, null);

        // Act — request with "en-US" (case-insensitive exact match)
        var settings = await GetTest<List<SettingDetailsDto>>(
            "/api/settings/system?language=en-US", HttpStatusCode.OK);

        // Assert
        Assert.NotNull(settings);
        var setting = settings.First(s => s.Key == "Test.FuzzyLang.E");
        Assert.Equal("english", setting.Value);
    }

    [Fact]
    public async Task GetSystemSetting_ByKey_WithFuzzyLanguage()
    {
        // Arrange — store generic and "ru" override
        var generalUrl = "/api/settings/system/Test.FuzzyLang.F?value=default";
        await Request(HttpMethod.Put, generalUrl, null);

        var ruUrl = "/api/settings/system/Test.FuzzyLang.F?value=russian&language=ru";
        await Request(HttpMethod.Put, ruUrl, null);

        // Act — single-key lookup with ru-RU
        var setting = await GetTest<SettingDetailsDto>(
            "/api/settings/system/Test.FuzzyLang.F?language=ru-RU", HttpStatusCode.OK);

        // Assert
        Assert.NotNull(setting);
        Assert.Equal("russian", setting.Value);
    }

    [Fact]
    public async Task FindSettingsByKeys_WithFuzzyLanguage()
    {
        // Arrange — store generic and "es" override for a config setting
        using var scope = App.Services.CreateScope();
        var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();

        await settingService.SetSystemSettingAsync("Test.FuzzyLang.G", "default");
        await settingService.SetSystemSettingAsync("Test.FuzzyLang.G", "spanish", "es");

        // Act — FindSettingsByKeysAsync with language "es-MX"
        var settings = await settingService.FindSettingsByKeysAsync(
            new[] { "Test.FuzzyLang.G" }, userId: null, language: "es-MX");

        // Assert — fuzzy match "es" for "es-MX"
        Assert.Single(settings);
        Assert.Equal("spanish", settings[0].Value);
    }

    [Fact]
    public async Task GetEffectiveUserSettings_WithFuzzyLanguage()
    {
        // Arrange — create a system setting with "de" override
        using var scope = App.Services.CreateScope();
        var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();

        await settingService.SetSystemSettingAsync("Test.FuzzyLang.H", "default");
        await settingService.SetSystemSettingAsync("Test.FuzzyLang.H", "german", "de");

        // Act — get effective settings for user with language "de-AT"
        var settings = await settingService.GetEffectiveUserSettingEntitiesAsync("some-user-id", "de-AT");

        // Assert — fuzzy match "de" for "de-AT"
        var setting = settings.First(s => s.Key == "Test.FuzzyLang.H");
        Assert.Equal("german", setting.Value);
    }

    [Fact]
    public async Task GetUserSettings_Endpoint_WithFuzzyLanguage()
    {
        // Arrange — store generic and "ja" override
        var generalUrl = "/api/settings/system/Test.FuzzyLang.I?value=default";
        await Request(HttpMethod.Put, generalUrl, null);

        var jaUrl = "/api/settings/system/Test.FuzzyLang.I?value=japanese&language=ja";
        await Request(HttpMethod.Put, jaUrl, null);

        // Act — user endpoint with language "ja-JP"
        var result = await GetTest<Dictionary<string, SettingValueDto>>(
            "/api/settings/user?language=ja-JP", HttpStatusCode.OK);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("Test.FuzzyLang.I"));
        Assert.Equal("japanese", result["Test.FuzzyLang.I"].Value);
    }

    [Fact]
    public async Task GetUserSetting_Endpoint_WithFuzzyLanguage()
    {
        // Arrange — store generic and "pt" override
        var generalUrl = "/api/settings/system/Test.FuzzyLang.J?value=default";
        await Request(HttpMethod.Put, generalUrl, null);

        var ptUrl = "/api/settings/system/Test.FuzzyLang.J?value=portuguese&language=pt";
        await Request(HttpMethod.Put, ptUrl, null);

        // Act — single-key user endpoint with language "pt-BR"
        var result = await GetTest<SettingValueDto>(
            "/api/settings/user/Test.FuzzyLang.J?language=pt-BR", HttpStatusCode.OK);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("portuguese", result.Value);
    }
}
