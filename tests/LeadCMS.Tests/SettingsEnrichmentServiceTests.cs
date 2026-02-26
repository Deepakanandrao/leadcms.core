// <copyright file="SettingsEnrichmentServiceTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Constants;
using LeadCMS.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Tests;

public class SettingsEnrichmentServiceTests : BaseTest
{
    [Fact]
    public async Task EnrichWithContentValidationSettingsAsync_HandlesNullValues()
    {
        // Arrange
        using var scope = App.Services.CreateScope();
        var enrichmentService = scope.ServiceProvider.GetRequiredService<ISettingsEnrichmentService>();

        var settings = new List<Setting>
        {
            new Setting { Key = SettingKeys.MinTitleLength, Value = null }, // Null value - should be replaced
            new Setting { Key = SettingKeys.MaxTitleLength, Value = "50" },  // Non-null value - should be kept
            // MinDescriptionLength missing - should be added
            // MaxDescriptionLength missing - should be added
        };

        // Act
        await enrichmentService.EnrichWithContentValidationSettingsAsync(settings);

        // Assert
        Assert.Equal("10", settings.First(s => s.Key == SettingKeys.MinTitleLength).Value); // Should use default since was null
        Assert.Equal("50", settings.First(s => s.Key == SettingKeys.MaxTitleLength).Value); // Should keep existing value
        Assert.Equal("20", settings.First(s => s.Key == SettingKeys.MinDescriptionLength).Value); // Should add default
        Assert.Equal("155", settings.First(s => s.Key == SettingKeys.MaxDescriptionLength).Value); // Should add default
    }

    [Fact]
    public async Task EnrichWithIdentitySettingsAsync_HandlesNullValues()
    {
        // Arrange
        using var scope = App.Services.CreateScope();
        var enrichmentService = scope.ServiceProvider.GetRequiredService<ISettingsEnrichmentService>();

        var settings = new List<Setting>
        {
            new Setting { Key = SettingKeys.RequireDigit, Value = null }, // Null value - should be replaced
            new Setting { Key = SettingKeys.RequireUppercase, Value = "false" }, // Non-null value - should be kept
            // Other settings missing - should be added with defaults
        };

        // Act
        await enrichmentService.EnrichWithIdentitySettingsAsync(settings);

        // Assert
        Assert.Equal("true", settings.First(s => s.Key == SettingKeys.RequireDigit).Value); // Should use default since was null
        Assert.Equal("false", settings.First(s => s.Key == SettingKeys.RequireUppercase).Value); // Should keep existing value
        Assert.Equal("true", settings.First(s => s.Key == SettingKeys.RequireLowercase).Value); // Should add default
        Assert.Equal("true", settings.First(s => s.Key == SettingKeys.RequireNonAlphanumeric).Value); // Should add default
        Assert.Equal("6", settings.First(s => s.Key == SettingKeys.RequiredLength).Value); // Should add default
        Assert.Equal("1", settings.First(s => s.Key == SettingKeys.RequiredUniqueChars).Value); // Should add default
    }
}