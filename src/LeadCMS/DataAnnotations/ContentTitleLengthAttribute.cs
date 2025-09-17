// <copyright file="ContentTitleLengthAttribute.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using LeadCMS.Configuration;
using LeadCMS.Constants;
using LeadCMS.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LeadCMS.DataAnnotations;

/// <summary>
/// Validates the maximum length of content title based on configured limits.
/// Checks runtime settings first, then falls back to configuration defaults.
/// Uses SEO-optimized default of 60 characters if no configuration is available.
/// </summary>
public class ContentTitleLengthAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string title)
        {
            return ValidationResult.Success;
        }

        var maxLength = GetMaxLength(validationContext);

        if (title.Length > maxLength)
        {
            return new ValidationResult($"Title must not exceed {maxLength} characters. Current length: {title.Length}");
        }

        return ValidationResult.Success;
    }

    private int GetMaxLength(ValidationContext validationContext)
    {
        // Try to get runtime setting first
        var settingService = validationContext.GetService(typeof(ISettingService)) as ISettingService;
        if (settingService != null)
        {
            try
            {
                var runtimeSetting = settingService.GetSystemSettingAsync(SettingKeys.MaxTitleLength).GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(runtimeSetting) && int.TryParse(runtimeSetting, out var runtimeLength) && runtimeLength > 0)
                {
                    return runtimeLength;
                }
            }
            catch
            {
                // Fall back to configuration if runtime setting fails
            }
        }

        // Fall back to configuration
        var options = validationContext.GetService(typeof(IOptions<ContentConfig>)) as IOptions<ContentConfig>;
        return options?.Value?.MaxTitleLength ?? 60; // SEO-optimized default
    }
}