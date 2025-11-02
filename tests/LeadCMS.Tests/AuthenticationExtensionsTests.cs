// <copyright file="AuthenticationExtensionsTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LeadCMS.DTOs;
using LeadCMS.Services;
using LeadCMS.Tests.TestEntities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LeadCMS.Tests;

public class AuthenticationExtensionsTests : BaseTestAutoLogin
{
    [Fact]
    public async Task DeviceAuthFlow_InitiateDevice_ShouldReturnValidDeviceCode()
    {
        // Act
        var response = await Request(HttpMethod.Post, "/api/identity/device/initiate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var deviceAuth = JsonSerializer.Deserialize<DeviceAuthInitiateDto>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        deviceAuth.Should().NotBeNull();
        deviceAuth!.DeviceCode.Should().NotBeNullOrEmpty();
        deviceAuth.UserCode.Should().NotBeNullOrEmpty();
        deviceAuth.UserCode.Should().MatchRegex(@"^[A-Z0-9]{4}-[A-Z0-9]{4}$");
        deviceAuth.VerificationUri.Should().NotBeNullOrEmpty();
        deviceAuth.VerificationUriComplete.Should().Contain(deviceAuth.UserCode);
        deviceAuth.ExpiresIn.Should().BeGreaterThan(0);
        deviceAuth.Interval.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DeviceAuthFlow_PollWithInvalidDeviceCode_ShouldReturnExpired()
    {
        // Arrange
        var pollRequest = new { deviceCode = "invalid_device_code" };

        // Act
        var response = await Request(HttpMethod.Post, "/api/identity/device/poll", pollRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("expired_token");
    }

    [Fact]
    public async Task DeviceAuthFlow_VerifyWithInvalidUserCode_ShouldReturnBadRequest()
    {
        // Arrange
        var verifyRequest = new { userCode = "INVALID-CODE" };

        // Act
        var response = await Request(HttpMethod.Post, "/api/identity/device/verify", verifyRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("invalid_user_code");
    }

    [Fact]
    public async Task TokenExchange_WithInvalidMicrosoftToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var tokenRequest = new { microsoftToken = "invalid.jwt.token" };

        // Act
        var response = await Request(HttpMethod.Post, "/api/identity/exchange-token", tokenRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeviceAuthService_GeneratesValidUserCodes()
    {
        // Arrange
        using var scope = Program.GetApp()!.Services.CreateScope();
        var deviceAuthService = scope.ServiceProvider.GetRequiredService<IDeviceAuthService>();

        // Act
        var deviceAuth = await deviceAuthService.InitiateDeviceAuthAsync("https://test.com");

        // Assert
        deviceAuth.Should().NotBeNull();
        deviceAuth.UserCode.Should().MatchRegex(@"^[A-Z0-9]{4}-[A-Z0-9]{4}$");
        deviceAuth.DeviceCode.Should().StartWith("device_");
        deviceAuth.ExpiresIn.Should().Be(900); // 15 minutes
        deviceAuth.Interval.Should().Be(5); // 5 seconds
    }

    [Fact]
    public async Task DeviceAuthService_PollNonExistentDevice_ReturnsExpired()
    {
        // Arrange
        using var scope = Program.GetApp()!.Services.CreateScope();
        var deviceAuthService = scope.ServiceProvider.GetRequiredService<IDeviceAuthService>();

        // Act
        var result = await deviceAuthService.PollDeviceAuthAsync("device_nonexistent");

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(DeviceAuthStatus.Expired);
        result.Token.Should().BeNull();
    }

    [Fact]
    public async Task DeviceAuthService_VerifyInvalidUserCode_ReturnsFalse()
    {
        // Arrange
        using var scope = Program.GetApp()!.Services.CreateScope();
        var deviceAuthService = scope.ServiceProvider.GetRequiredService<IDeviceAuthService>();
        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.Email, "test@example.com"),
        };

        // Act
        var result = await deviceAuthService.VerifyUserCodeAsync("INVALID-CODE", claims);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeviceAuthService_CompleteFlow_WorksCorrectly()
    {
        // Arrange
        using var scope = Program.GetApp()!.Services.CreateScope();
        var deviceAuthService = scope.ServiceProvider.GetRequiredService<IDeviceAuthService>();
        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.Email, "test@example.com"),
            new(System.Security.Claims.ClaimTypes.NameIdentifier, "test-user-id"),
        };

        // Act - Initiate
        var deviceAuth = await deviceAuthService.InitiateDeviceAuthAsync("https://test.com");

        // Act - Verify
        var deviceCode = await deviceAuthService.VerifyUserCodeAsync(deviceAuth.UserCode, claims);
        deviceCode.Should().NotBeNull();
        deviceCode.Should().Be(deviceAuth.DeviceCode);

        // Act - Complete
        var completed = await deviceAuthService.CompleteDeviceAuthAsync(deviceCode!, "test-token", DateTime.UtcNow.AddHours(1));
        completed.Should().BeTrue();

        // Act - Poll
        var pollResult = await deviceAuthService.PollDeviceAuthAsync(deviceAuth.DeviceCode);

        // Assert
        pollResult.Should().NotBeNull();
        pollResult.Status.Should().Be(DeviceAuthStatus.Completed);
        pollResult.Token.Should().Be("test-token");
        pollResult.TokenExpiration.Should().NotBeNull();
    }
}