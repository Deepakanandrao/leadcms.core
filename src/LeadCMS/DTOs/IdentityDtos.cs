// <copyright file="IdentityDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace LeadCMS.DTOs;

public class LoginDto
{
    [Required]
    [EmailAddress]
    required public string Email { get; set; }

    [Required]
    required public string Password { get; set; }
}

public class JWTokenDto
{
    [Required]
    required public string Token { get; set; }

    [Required]
    required public DateTime Expiration { get; set; }
}

public class TokenExchangeDto
{
    [Required]
    required public string MicrosoftToken { get; set; }
}

public class DeviceAuthInitiateDto
{
    [Required]
    required public string DeviceCode { get; set; }

    [Required]
    required public string UserCode { get; set; }

    [Required]
    required public string VerificationUri { get; set; }

    [Required]
    required public string VerificationUriComplete { get; set; }

    [Required]
    required public int ExpiresIn { get; set; }

    [Required]
    required public int Interval { get; set; }
}

public class DeviceAuthPollDto
{
    [Required]
    required public string DeviceCode { get; set; }
}

public class DeviceAuthVerificationDto
{
    [Required]
    required public string UserCode { get; set; }
}