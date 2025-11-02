// <copyright file="IDeviceAuthService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Security.Claims;
using LeadCMS.DTOs;
using LeadCMS.Services;

namespace LeadCMS.Interfaces;

/// <summary>
/// Service for managing device-based authentication flow similar to OAuth2 device flow.
/// </summary>
public interface IDeviceAuthService
{
    /// <summary>
    /// Initiates a device authentication flow.
    /// </summary>
    /// <param name="baseUrl">Base URL for generating verification URIs.</param>
    /// <returns>Device authentication initiation data.</returns>
    Task<DeviceAuthInitiateDto> InitiateDeviceAuthAsync(string baseUrl);

    /// <summary>
    /// Polls the status of a device authentication request.
    /// </summary>
    /// <param name="deviceCode">The device code to poll.</param>
    /// <returns>Current status of the device authentication.</returns>
    Task<DeviceAuthPollResult> PollDeviceAuthAsync(string deviceCode);

    /// <summary>
    /// Verifies a user code and associates it with user claims.
    /// </summary>
    /// <param name="userCode">The user code to verify.</param>
    /// <param name="userClaims">Claims of the authenticated user.</param>
    /// <returns>Device code if verification was successful, null otherwise.</returns>
    Task<string?> VerifyUserCodeAsync(string userCode, List<Claim> userClaims);

    /// <summary>
    /// Completes the device authentication by providing the final token.
    /// </summary>
    /// <param name="deviceCode">The device code.</param>
    /// <param name="token">The JWT token for the authenticated user.</param>
    /// <param name="tokenExpiration">Token expiration time.</param>
    /// <returns>True if completion was successful.</returns>
    Task<bool> CompleteDeviceAuthAsync(string deviceCode, string token, DateTime tokenExpiration);

    /// <summary>
    /// Denies a device authentication request.
    /// </summary>
    /// <param name="userCode">The user code to deny.</param>
    /// <param name="reason">Reason for denial.</param>
    /// <returns>True if denial was successful.</returns>
    Task<bool> DenyDeviceAuthAsync(string userCode, string reason);
}