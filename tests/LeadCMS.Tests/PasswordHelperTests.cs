// <copyright file="PasswordHelperTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using FluentAssertions;
using LeadCMS.Configuration;
using LeadCMS.Helpers;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace LeadCMS.Tests;

public class PasswordHelperTests
{
    [Fact]
    public void GenerateStrongPassword_WithDefaultConfig_ShouldMeetDefaultRequirements()
    {
        // Act
        var password = PasswordHelper.GenerateStrongPassword();

        // Assert
        password.Should().NotBeNullOrEmpty();
        password.Length.Should().BeGreaterOrEqualTo(16);
        password.Should().MatchRegex(@".*[a-z].*", "should contain lowercase");
        password.Should().MatchRegex(@".*[A-Z].*", "should contain uppercase");
        password.Should().MatchRegex(@".*\d.*", "should contain digit");
        password.Should().MatchRegex(@".*[^a-zA-Z0-9].*", "should contain special character");
    }

    [Fact]
    public void GenerateStrongPassword_WithCustomConfig_ShouldRespectConfiguration()
    {
        // Arrange
        var config = new IdentityConfig
        {
            RequireDigit = false,
            RequireUppercase = true,
            RequireLowercase = true,
            RequireNonAlphanumeric = false,
            RequiredLength = 12,
        };

        // Act
        var password = PasswordHelper.GenerateStrongPassword(config, 12);

        // Assert
        password.Should().NotBeNullOrEmpty();
        password.Length.Should().Be(12);
        password.Should().MatchRegex(@".*[a-z].*", "should contain lowercase");
        password.Should().MatchRegex(@".*[A-Z].*", "should contain uppercase");
        // Since digits and special chars are not required, the password may still contain them
        // Just verify the length and required character types
    }

    [Fact]
    public void GenerateStrongPassword_WithMinimalRequirements_ShouldWork()
    {
        // Arrange
        var config = new IdentityConfig
        {
            RequireDigit = false,
            RequireUppercase = false,
            RequireLowercase = false,
            RequireNonAlphanumeric = false,
            RequiredLength = 6,
        };

        // Act
        var password = PasswordHelper.GenerateStrongPassword(config, 6);

        // Assert
        password.Should().NotBeNullOrEmpty();
        password.Length.Should().Be(6);
    }

    [Fact]
    public void GenerateStrongPassword_WithHighRequirements_ShouldMeetAllCriteria()
    {
        // Arrange
        var config = new IdentityConfig
        {
            RequireDigit = true,
            RequireUppercase = true,
            RequireLowercase = true,
            RequireNonAlphanumeric = true,
            RequiredLength = 20,
        };

        // Act
        var password = PasswordHelper.GenerateStrongPassword(config, 20);

        // Assert
        password.Should().NotBeNullOrEmpty();
        password.Length.Should().Be(20);
        password.Should().MatchRegex(@".*[a-z].*", "should contain lowercase");
        password.Should().MatchRegex(@".*[A-Z].*", "should contain uppercase");
        password.Should().MatchRegex(@".*\d.*", "should contain digit");
        password.Should().MatchRegex(@".*[^a-zA-Z0-9].*", "should contain special character");
    }

    [Fact]
    public void GenerateStrongPassword_ShouldValidateAgainstAspNetIdentityPasswordOptions()
    {
        // Arrange
        var config = new IdentityConfig
        {
            RequireDigit = true,
            RequireUppercase = true,
            RequireLowercase = true,
            RequireNonAlphanumeric = true,
            RequiredLength = 10,
            RequiredUniqueChars = 5,
        };

        var passwordOptions = new PasswordOptions
        {
            RequireDigit = config.RequireDigit,
            RequireUppercase = config.RequireUppercase,
            RequireLowercase = config.RequireLowercase,
            RequireNonAlphanumeric = config.RequireNonAlphanumeric,
            RequiredLength = config.RequiredLength,
            RequiredUniqueChars = config.RequiredUniqueChars,
        };

        // Act
        var password = PasswordHelper.GenerateStrongPassword(config, 10);

        // Assert - Simulate ASP.NET Identity validation
        password.Should().NotBeNullOrEmpty();
        password.Length.Should().BeGreaterOrEqualTo(passwordOptions.RequiredLength);

        if (passwordOptions.RequireDigit)
        {
            password.Should().MatchRegex(@".*\d.*", "should contain digit");
        }

        if (passwordOptions.RequireUppercase)
        {
            password.Should().MatchRegex(@".*[A-Z].*", "should contain uppercase");
        }

        if (passwordOptions.RequireLowercase)
        {
            password.Should().MatchRegex(@".*[a-z].*", "should contain lowercase");
        }

        if (passwordOptions.RequireNonAlphanumeric)
        {
            password.Should().MatchRegex(@".*[^a-zA-Z0-9].*", "should contain special character");
        }

        // Check unique characters
        password.Distinct().Count().Should().BeGreaterOrEqualTo(passwordOptions.RequiredUniqueChars);
    }

    [Fact]
    public void GenerateStrongPassword_WithRealIdentityConfig_ShouldPassRealValidation()
    {
        // This test demonstrates that our PasswordHelper actually generates passwords
        // that will pass ASP.NET Identity validation with real configuration settings

        // Arrange - Use realistic configuration like in a real application
        var config = new IdentityConfig
        {
            RequireDigit = true,
            RequireUppercase = true,
            RequireLowercase = true,
            RequireNonAlphanumeric = true,
            RequiredLength = 8,
            RequiredUniqueChars = 4,
        };

        // Act - Generate 10 passwords to ensure consistency
        for (int i = 0; i < 10; i++)
        {
            var password = PasswordHelper.GenerateStrongPassword(config);

            // Assert - Each generated password should meet all requirements
            password.Should().NotBeNullOrEmpty($"iteration {i}");
            password.Length.Should().BeGreaterOrEqualTo(config.RequiredLength, $"iteration {i}");
            password.Should().MatchRegex(@".*\d.*", $"iteration {i} should contain digit");
            password.Should().MatchRegex(@".*[A-Z].*", $"iteration {i} should contain uppercase");
            password.Should().MatchRegex(@".*[a-z].*", $"iteration {i} should contain lowercase");
            password.Should().MatchRegex(@".*[^a-zA-Z0-9].*", $"iteration {i} should contain special character");
            password.Distinct().Count().Should().BeGreaterOrEqualTo(config.RequiredUniqueChars, $"iteration {i} should have enough unique characters");
        }
    }
}