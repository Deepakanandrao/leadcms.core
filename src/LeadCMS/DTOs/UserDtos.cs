// <copyright file="UserDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CsvHelper.Configuration.Attributes;
using LeadCMS.Infrastructure;

namespace LeadCMS.DTOs;

public class UserCreateDto : UserBaseDto
{
    public string? Password { get; set; }

    public bool GeneratePassword { get; set; } = false;

    public bool SendPasswordEmail { get; set; } = false;

    public string Language { get; set; } = string.Empty;
}

public class UserUpdateDto : IPatchDto
{
    [Ignore]
    [JsonIgnore]
    public HashSet<string> NullProperties { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public string? Email { get; set; }

    public string? UserName { get; set; }

    public string? DisplayName { get; set; }

    public string? AvatarUrl { get; set; }

    public Dictionary<string, object>? Data { get; set; }

    public string? Password { get; set; }

    public bool GeneratePassword { get; set; } = false;

    public bool SendPasswordEmail { get; set; } = false;

    public string Language { get; set; } = string.Empty;
}

public class UserDetailsDto : UserBaseDto
{
    public string Id { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? LastTimeLoggedIn { get; set; }

    public string AvatarUrl { get; set; } = string.Empty;
}

public class UserBaseDto
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string DisplayName { get; set; } = string.Empty;

    public Dictionary<string, object>? Data { get; set; }
}

public class ChangePasswordDto
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    public string NewPassword { get; set; } = string.Empty;
}

public class ForgotPasswordDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;
}

public class ResetPasswordDto
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    public string NewPassword { get; set; } = string.Empty;
}