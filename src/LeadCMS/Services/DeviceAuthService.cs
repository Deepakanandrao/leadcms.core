// <copyright file="DeviceAuthService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Security.Claims;
using LeadCMS.DTOs;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace LeadCMS.Services;

/// <summary>
/// Service for managing device-based authentication flow similar to OAuth2 device flow.
/// </summary>
public class DeviceAuthService : IDeviceAuthService
{
    private const string DeviceCodePrefix = "device_";
    private const int DeviceCodeExpirationMinutes = 15;
    private const int PollIntervalSeconds = 5;

    private readonly IMemoryCache cache;
    private readonly ILogger<DeviceAuthService> logger;

    public DeviceAuthService(IMemoryCache cache, ILogger<DeviceAuthService> logger)
    {
        this.cache = cache;
        this.logger = logger;
    }

    public Task<DeviceAuthInitiateDto> InitiateDeviceAuthAsync(string baseUrl)
    {
        var deviceCode = DeviceCodePrefix + UidHelper.Generate(32);
        var userCode = GenerateUserFriendlyCode();

        var deviceAuthData = new DeviceAuthData
        {
            DeviceCode = deviceCode,
            UserCode = userCode,
            CreatedAt = DateTime.UtcNow,
            Status = DeviceAuthStatus.Pending,
        };

        // Store device auth data in cache
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(DeviceCodeExpirationMinutes),
        };

        cache.Set(deviceCode, deviceAuthData, cacheOptions);
        cache.Set($"usercode_{userCode}", deviceCode, cacheOptions);

        var verificationUri = $"{baseUrl.TrimEnd('/')}/auth/device-verify";
        var verificationUriComplete = $"{verificationUri}?user_code={userCode}";

        logger.LogInformation(
            "Device auth initiated: DeviceCode={DeviceCode}, UserCode={UserCode}",
            deviceCode,
            userCode);

        return Task.FromResult(new DeviceAuthInitiateDto
        {
            DeviceCode = deviceCode,
            UserCode = userCode,
            VerificationUri = verificationUri,
            VerificationUriComplete = verificationUriComplete,
            ExpiresIn = DeviceCodeExpirationMinutes * 60,
            Interval = PollIntervalSeconds,
        });
    }

    public Task<DeviceAuthPollResult> PollDeviceAuthAsync(string deviceCode)
    {
        if (!cache.TryGetValue(deviceCode, out DeviceAuthData? authData) || authData == null)
        {
            return Task.FromResult(new DeviceAuthPollResult
            {
                Status = DeviceAuthStatus.Expired,
                ErrorDescription = "Device code has expired or is invalid",
            });
        }

        return Task.FromResult(new DeviceAuthPollResult
        {
            Status = authData.Status,
            Token = authData.Token,
            TokenExpiration = authData.TokenExpiration,
            ErrorDescription = authData.ErrorDescription,
        });
    }

    public Task<string?> VerifyUserCodeAsync(string userCode, List<Claim> userClaims)
    {
        var userCodeKey = $"usercode_{userCode}";

        if (!cache.TryGetValue(userCodeKey, out string? deviceCode) || string.IsNullOrEmpty(deviceCode))
        {
            logger.LogWarning("Invalid or expired user code: {UserCode}", userCode);
            return Task.FromResult<string?>(null);
        }

        if (!cache.TryGetValue(deviceCode, out DeviceAuthData? authData) || authData == null)
        {
            logger.LogWarning("Device auth data not found for device code: {DeviceCode}", deviceCode);
            return Task.FromResult<string?>(null);
        }

        if (authData.Status != DeviceAuthStatus.Pending)
        {
            logger.LogWarning(
                "Device auth already processed: {DeviceCode}, Status: {Status}",
                deviceCode,
                authData.Status);
            return Task.FromResult<string?>(null);
        }

        // Update the device auth data with user authentication
        authData.Status = DeviceAuthStatus.Authorized;
        authData.UserClaims = userClaims;
        authData.AuthorizedAt = DateTime.UtcNow;

        // Update cache
        cache.Set(deviceCode, authData, TimeSpan.FromMinutes(DeviceCodeExpirationMinutes));

        logger.LogInformation(
            "Device auth verified successfully: DeviceCode={DeviceCode}, UserCode={UserCode}",
            deviceCode,
            userCode);

        return Task.FromResult<string?>(deviceCode);
    }

    public Task<bool> CompleteDeviceAuthAsync(string deviceCode, string token, DateTime tokenExpiration)
    {
        if (!cache.TryGetValue(deviceCode, out DeviceAuthData? authData) || authData == null)
        {
            return Task.FromResult(false);
        }

        if (authData.Status != DeviceAuthStatus.Authorized)
        {
            return Task.FromResult(false);
        }

        // Update with token information
        authData.Status = DeviceAuthStatus.Completed;
        authData.Token = token;
        authData.TokenExpiration = tokenExpiration;
        authData.CompletedAt = DateTime.UtcNow;

        // Update cache
        cache.Set(deviceCode, authData, TimeSpan.FromMinutes(5)); // Keep for 5 more minutes for polling

        logger.LogInformation("Device auth completed: DeviceCode={DeviceCode}", deviceCode);

        return Task.FromResult(true);
    }

    public Task<bool> DenyDeviceAuthAsync(string userCode, string reason)
    {
        var userCodeKey = $"usercode_{userCode}";

        if (!cache.TryGetValue(userCodeKey, out string? deviceCode) || string.IsNullOrEmpty(deviceCode))
        {
            return Task.FromResult(false);
        }

        if (!cache.TryGetValue(deviceCode, out DeviceAuthData? authData) || authData == null)
        {
            return Task.FromResult(false);
        }

        authData.Status = DeviceAuthStatus.Denied;
        authData.ErrorDescription = reason;
        authData.AuthorizedAt = DateTime.UtcNow;

        // Update cache
        cache.Set(deviceCode, authData, TimeSpan.FromMinutes(5)); // Keep for 5 more minutes for polling

        logger.LogInformation(
            "Device auth denied: DeviceCode={DeviceCode}, UserCode={UserCode}, Reason={Reason}",
            deviceCode,
            userCode,
            reason);

        return Task.FromResult(true);
    }

    private static string GenerateUserFriendlyCode()
    {
        // Generate a user-friendly code (like GitHub's device flow: XXXX-XXXX)
        var random = new Random();
        var chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Exclude confusing characters

        var part1 = new string(Enumerable.Range(0, 4).Select(_ => chars[random.Next(chars.Length)]).ToArray());
        var part2 = new string(Enumerable.Range(0, 4).Select(_ => chars[random.Next(chars.Length)]).ToArray());

        return $"{part1}-{part2}";
    }
}

/// <summary>
/// Device authentication data stored in cache.
/// </summary>
public class DeviceAuthData
{
    public string DeviceCode { get; set; } = string.Empty;

    public string UserCode { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? AuthorizedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DeviceAuthStatus Status { get; set; }

    public List<Claim>? UserClaims { get; set; }

    public string? Token { get; set; }

    public DateTime? TokenExpiration { get; set; }

    public string? ErrorDescription { get; set; }
}

/// <summary>
/// Device authentication status.
/// </summary>
public enum DeviceAuthStatus
{
    Pending,
    Authorized,
    Completed,
    Denied,
    Expired,
}

/// <summary>
/// Result of device authentication polling.
/// </summary>
public class DeviceAuthPollResult
{
    public DeviceAuthStatus Status { get; set; }

    public string? Token { get; set; }

    public DateTime? TokenExpiration { get; set; }

    public string? ErrorDescription { get; set; }
}