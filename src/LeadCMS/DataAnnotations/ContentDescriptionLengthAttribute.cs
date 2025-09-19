// <copyright file="ContentDescriptionLengthAttribute.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using LeadCMS.Configuration;
using LeadCMS.Constants;
using LeadCMS.Interfaces;
using Microsoft.Extensions.Options;

namespace LeadCMS.DataAnnotations;

/// <summary>
/// Validates the minimum and maximum length of content description based on configured limits.
/// Checks runtime settings first, then falls back to configuration defaults.
/// Uses SEO-optimized default of 155 characters for max and 1 for min if no configuration is available.
/// </summary>
public class ContentDescriptionLengthAttribute : ValidationAttribute
{
    /// <summary>
    /// Validates that the description does not exceed the configured maximum length and meets the minimum length.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <returns>Validation result.</returns>
    protected override ValidationResult IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string description)
        {
            return ValidationResult.Success!;
        }

        var (minLength, maxLength) = GetMinMaxLength(validationContext);

        if (description.Length < minLength)
        {
            return new ValidationResult($"Description must be at least {minLength} characters.");
        }

        if (description.Length > maxLength)
        {
            return new ValidationResult($"Description cannot exceed {maxLength} characters for SEO optimization.");
        }

        return ValidationResult.Success!;
    }

    private (int minLength, int maxLength) GetMinMaxLength(ValidationContext validationContext)
    {
        int minLength = 1; // Default min length
        int maxLength = 155; // SEO-optimized default max length

        // Try to get runtime setting first
        var settingService = validationContext.GetService(typeof(ISettingService)) as ISettingService;
        if (settingService != null)
        {
            try
            {
                var runtimeMax = settingService.GetSystemSettingAsync(SettingKeys.MaxDescriptionLength).GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(runtimeMax) && int.TryParse(runtimeMax, out var runtimeMaxLength) && runtimeMaxLength > 0)
                {
                    maxLength = runtimeMaxLength;
                }

                var runtimeMin = settingService.GetSystemSettingAsync(SettingKeys.MinDescriptionLength).GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(runtimeMin) && int.TryParse(runtimeMin, out var runtimeMinLength) && runtimeMinLength > 0)
                {
                    minLength = runtimeMinLength;
                }
            }
            catch
            {
                // Fall back to configuration if runtime setting fails
            }
        }

        // Fall back to configuration
        var configuration = validationContext.GetService(typeof(IOptions<ContentConfig>)) as IOptions<ContentConfig>;
        if (configuration?.Value != null)
        {
            if (configuration.Value.MaxDescriptionLength > 0)
            {
                maxLength = configuration.Value.MaxDescriptionLength;
            }

            if (configuration.Value.MinDescriptionLength > 0)
            {
                minLength = configuration.Value.MinDescriptionLength;
            }
        }

        return (minLength, maxLength);
    }
}