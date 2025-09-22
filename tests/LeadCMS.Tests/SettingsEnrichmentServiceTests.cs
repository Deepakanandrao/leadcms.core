// <copyright file="SettingsEnrichmentServiceTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Constants;
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

        var settings = new Dictionary<string, string?>
        {
            { SettingKeys.MinTitleLength, null }, // Null value - should be replaced
            { SettingKeys.MaxTitleLength, "50" },  // Non-null value - should be kept
            // MinDescriptionLength missing - should be added
            // MaxDescriptionLength missing - should be added
        };

        // Act
        await enrichmentService.EnrichWithContentValidationSettingsAsync(settings);

        // Assert
        Assert.Equal("10", settings[SettingKeys.MinTitleLength]); // Should use default since was null
        Assert.Equal("50", settings[SettingKeys.MaxTitleLength]); // Should keep existing value
        Assert.Equal("20", settings[SettingKeys.MinDescriptionLength]); // Should add default
        Assert.Equal("155", settings[SettingKeys.MaxDescriptionLength]); // Should add default
    }

    [Fact]
    public async Task EnrichWithIdentitySettingsAsync_HandlesNullValues()
    {
        // Arrange
        using var scope = App.Services.CreateScope();
        var enrichmentService = scope.ServiceProvider.GetRequiredService<ISettingsEnrichmentService>();

        var settings = new Dictionary<string, string?>
        {
            { SettingKeys.RequireDigit, null }, // Null value - should be replaced
            { SettingKeys.RequireUppercase, "false" }, // Non-null value - should be kept
            // Other settings missing - should be added with defaults
        };

        // Act
        await enrichmentService.EnrichWithIdentitySettingsAsync(settings);

        // Assert
        Assert.Equal("true", settings[SettingKeys.RequireDigit]); // Should use default since was null
        Assert.Equal("false", settings[SettingKeys.RequireUppercase]); // Should keep existing value
        Assert.Equal("true", settings[SettingKeys.RequireLowercase]); // Should add default
        Assert.Equal("true", settings[SettingKeys.RequireNonAlphanumeric]); // Should add default
        Assert.Equal("6", settings[SettingKeys.RequiredLength]); // Should add default
        Assert.Equal("1", settings[SettingKeys.RequiredUniqueChars]); // Should add default
    }
}