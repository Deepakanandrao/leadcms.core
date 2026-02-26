// <copyright file="SettingListHelperLanguageTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;
using LeadCMS.Helpers;

namespace LeadCMS.Tests;

public class SettingListHelperLanguageTests
{
    [Fact]
    public void PickBestLanguageMatch_ExactMatch_WinsOverFuzzy()
    {
        var candidates = new List<Setting>
        {
            new Setting { Key = "k", Language = null, Value = "generic" },
            new Setting { Key = "k", Language = "ru", Value = "ru" },
            new Setting { Key = "k", Language = "ru-RU", Value = "ru-RU" },
        };

        var result = SettingListHelper.PickBestLanguageMatch(candidates, "ru-RU");

        Assert.NotNull(result);
        Assert.Equal("ru-RU", result.Value);
    }

    [Fact]
    public void PickBestLanguageMatch_FuzzyMatch_RuMatchesRuRU()
    {
        var candidates = new List<Setting>
        {
            new Setting { Key = "k", Language = null, Value = "generic" },
            new Setting { Key = "k", Language = "ru", Value = "ru" },
        };

        var result = SettingListHelper.PickBestLanguageMatch(candidates, "ru-RU");

        Assert.NotNull(result);
        Assert.Equal("ru", result.Value);
    }

    [Fact]
    public void PickBestLanguageMatch_FuzzyMatch_RuRUMatchesRu()
    {
        var candidates = new List<Setting>
        {
            new Setting { Key = "k", Language = null, Value = "generic" },
            new Setting { Key = "k", Language = "ru-RU", Value = "ru-RU" },
        };

        var result = SettingListHelper.PickBestLanguageMatch(candidates, "ru");

        Assert.NotNull(result);
        Assert.Equal("ru-RU", result.Value);
    }

    [Fact]
    public void PickBestLanguageMatch_NoMatch_FallsBackToGeneric()
    {
        var candidates = new List<Setting>
        {
            new Setting { Key = "k", Language = null, Value = "generic" },
            new Setting { Key = "k", Language = "fr", Value = "french" },
        };

        var result = SettingListHelper.PickBestLanguageMatch(candidates, "de");

        Assert.NotNull(result);
        Assert.Equal("generic", result.Value);
    }

    [Fact]
    public void PickBestLanguageMatch_NullLanguage_ReturnsGeneric()
    {
        var candidates = new List<Setting>
        {
            new Setting { Key = "k", Language = null, Value = "generic" },
            new Setting { Key = "k", Language = "ru", Value = "ru" },
        };

        var result = SettingListHelper.PickBestLanguageMatch(candidates, null);

        Assert.NotNull(result);
        Assert.Equal("generic", result.Value);
    }

    [Fact]
    public void PickBestLanguageMatch_EmptyList_ReturnsNull()
    {
        var result = SettingListHelper.PickBestLanguageMatch(new List<Setting>(), "ru");

        Assert.Null(result);
    }

    [Fact]
    public void PickBestLanguageMatch_CaseInsensitive()
    {
        var candidates = new List<Setting>
        {
            new Setting { Key = "k", Language = null, Value = "generic" },
            new Setting { Key = "k", Language = "EN-us", Value = "english" },
        };

        var result = SettingListHelper.PickBestLanguageMatch(candidates, "en-US");

        Assert.NotNull(result);
        Assert.Equal("english", result.Value);
    }

    [Fact]
    public void PickBestLanguageMatch_NoGeneric_ReturnsMatch()
    {
        var candidates = new List<Setting>
        {
            new Setting { Key = "k", Language = "ru", Value = "ru-only" },
        };

        var result = SettingListHelper.PickBestLanguageMatch(candidates, "ru-RU");

        Assert.NotNull(result);
        Assert.Equal("ru-only", result.Value);
    }

    [Fact]
    public void PickBestLanguageMatch_NoGenericNoMatch_ReturnsNull()
    {
        var candidates = new List<Setting>
        {
            new Setting { Key = "k", Language = "fr", Value = "french" },
        };

        var result = SettingListHelper.PickBestLanguageMatch(candidates, "de");

        Assert.Null(result);
    }

    [Fact]
    public void LanguageFamilyMatches_SamePrefix_ReturnsTrue()
    {
        Assert.True(SettingListHelper.LanguageFamilyMatches("ru", "ru-RU"));
        Assert.True(SettingListHelper.LanguageFamilyMatches("ru-RU", "ru"));
        Assert.True(SettingListHelper.LanguageFamilyMatches("en", "en-US"));
        Assert.True(SettingListHelper.LanguageFamilyMatches("EN", "en-us"));
    }

    [Fact]
    public void LanguageFamilyMatches_DifferentPrefix_ReturnsFalse()
    {
        Assert.False(SettingListHelper.LanguageFamilyMatches("ru", "en"));
        Assert.False(SettingListHelper.LanguageFamilyMatches("fr-FR", "de-DE"));
    }

    [Fact]
    public void LanguageFamilyMatches_NullValues_ReturnsFalse()
    {
        Assert.False(SettingListHelper.LanguageFamilyMatches(null, "ru"));
        Assert.False(SettingListHelper.LanguageFamilyMatches("ru", null));
        Assert.False(SettingListHelper.LanguageFamilyMatches(null, null));
    }
}
