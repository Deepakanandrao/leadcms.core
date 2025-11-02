// <copyright file="ITokenService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Security.Claims;
using LeadCMS.DTOs;

namespace LeadCMS.Interfaces;

/// <summary>
/// Service for managing JWT token operations and Microsoft token validation.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Validates a Microsoft token and extracts user information.
    /// </summary>
    /// <param name="microsoftToken">The Microsoft JWT token to validate.</param>
    /// <returns>Claims extracted from the validated token.</returns>
    Task<List<Claim>> ValidateMicrosoftTokenAsync(string microsoftToken);

    /// <summary>
    /// Exchanges a validated Microsoft token for an internal JWT token.
    /// </summary>
    /// <param name="microsoftToken">The Microsoft JWT token.</param>
    /// <returns>Internal JWT token response.</returns>
    Task<JWTokenDto> ExchangeTokenAsync(string microsoftToken);

    /// <summary>
    /// Generates an internal JWT token from user claims.
    /// </summary>
    /// <param name="userClaims">User claims to include in the token.</param>
    /// <returns>JWT token response.</returns>
    Task<JWTokenDto> GenerateTokenAsync(List<Claim> userClaims);

    /// <summary>
    /// Performs password-based login authentication.
    /// </summary>
    /// <param name="email">User email.</param>
    /// <param name="password">User password.</param>
    /// <returns>JWT token response.</returns>
    Task<JWTokenDto> LoginWithPasswordAsync(string email, string password);
}