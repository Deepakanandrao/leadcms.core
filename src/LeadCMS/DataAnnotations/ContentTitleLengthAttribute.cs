// <copyright file="ContentTitleLengthAttribute.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using LeadCMS.Configuration;
using LeadCMS.Constants;
using LeadCMS.Interfaces;
using Microsoft.Extensions.Options;

namespace LeadCMS.DataAnnotations;

public class ContentTitleLengthAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string title)
        {
            return ValidationResult.Success;
        }

        var (minLength, maxLength) = GetMinMaxLength(validationContext);

        if (title.Length < minLength)
        {
            return new ValidationResult($"Title must be at least {minLength} characters. Current length: {title.Length}");
        }

        if (title.Length > maxLength)
        {
            return new ValidationResult($"Title must not exceed {maxLength} characters. Current length: {title.Length}");
        }

        return ValidationResult.Success;
    }

    private (int minLength, int maxLength) GetMinMaxLength(ValidationContext validationContext)
    {
        int minLength = 10; // Default minimum
        int maxLength = 60; // Default maximum (SEO-optimized)

        var settingService = validationContext.GetService(typeof(ISettingService)) as ISettingService;
        if (settingService != null)
        {
            try
            {
                var runtimeMax = settingService.GetSystemSettingAsync(SettingKeys.MaxTitleLength).GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(runtimeMax) && int.TryParse(runtimeMax, out var runtimeMaxLength) && runtimeMaxLength > 0)
                {
                    maxLength = runtimeMaxLength;
                }

                var runtimeMin = settingService.GetSystemSettingAsync(SettingKeys.MinTitleLength).GetAwaiter().GetResult();
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

        var options = validationContext.GetService(typeof(IOptions<ContentConfig>)) as IOptions<ContentConfig>;
        if (options?.Value != null)
        {
            if (options.Value.MaxTitleLength > 0)
            {
                maxLength = options.Value.MaxTitleLength;
            }

            if (options.Value.MinTitleLength > 0)
            {
                minLength = options.Value.MinTitleLength;
            }
        }

        return (minLength, maxLength);
    }
}