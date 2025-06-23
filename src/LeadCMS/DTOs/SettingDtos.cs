// <copyright file="SettingDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace LeadCMS.DTOs;

public class SettingCreateDto
{
    [Required]
    [MaxLength(255)]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;

    public string? UserId { get; set; }
}

public class SettingUpdateDto
{
    [Required]
    public string Value { get; set; } = string.Empty;
}

public class SettingDetailsDto
{
    public int Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? UserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedById { get; set; }

    public string? UpdatedById { get; set; }

    public bool IsUserLevel => !string.IsNullOrEmpty(UserId);
}

public class SettingValueDto
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public bool IsUserLevel { get; set; }
}
