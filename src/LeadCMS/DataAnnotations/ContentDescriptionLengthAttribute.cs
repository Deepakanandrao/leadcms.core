// <copyright file="ContentDescriptionLengthAttribute.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using LeadCMS.Configuration;
using LeadCMS.Constants;
using LeadCMS.Interfaces;
using Microsoft.Extensions.Options;

namespace LeadCMS.DataAnnotations
{
    /// <summary>
    /// Validates the maximum length of content description based on configured limits.
    /// Checks runtime settings first, then falls back to configuration defaults.
    /// Uses SEO-optimized default of 155 characters if no configuration is available.
    /// </summary>
    public class ContentDescriptionLengthAttribute : ValidationAttribute
    {
        /// <summary>
        /// Validates that the description does not exceed the configured maximum length.
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

            var maxLength = GetMaxLength(validationContext);

            if (description.Length > maxLength)
            {
                return new ValidationResult($"Description cannot exceed {maxLength} characters for SEO optimization.");
            }

            return ValidationResult.Success!;
        }

        private int GetMaxLength(ValidationContext validationContext)
        {
            // Try to get runtime setting first
            var settingService = validationContext.GetService(typeof(ISettingService)) as ISettingService;
            if (settingService != null)
            {
                try
                {
                    var runtimeSetting = settingService.GetSystemSettingAsync(SettingKeys.MaxDescriptionLength).GetAwaiter().GetResult();
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
            var configuration = validationContext.GetService(typeof(IOptions<ContentConfig>)) as IOptions<ContentConfig>;
            return configuration?.Value?.MaxDescriptionLength ?? 155; // SEO-optimized default
        }
    }
}