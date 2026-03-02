// <copyright file="ContactUsDto.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using LeadCMS.Plugin.Site.Serialization;
using Microsoft.AspNetCore.Http;

namespace LeadCMS.Plugin.Site.DTOs
{
    public class ContactUsDto : ClientLocaleAwareDto
    {
        private string? email;

        public IFormFile? Attachment { get; set; }

        /// <summary>
        /// Gets or sets the notification title or topic provided by the client.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the template name for internal lead notification emails.
        /// If not provided, the default template is used.
        /// </summary>
        public string? NotificationType { get; set; }

        /// <summary>
        /// Gets or sets the template name for acknowledgment emails.
        /// If not provided, the default template is used.
        /// </summary>
        public string? AcknowledgmentType { get; set; }

        /// <summary>
        /// Gets or sets the source page URL for the contact submission.
        /// </summary>
        public string? PageUrl { get; set; }

        /// <summary>
        /// Gets or sets the full name. When set and FirstName/LastName are not
        /// explicitly provided, the value is split into FirstName, MiddleName
        /// and LastName:
        /// <list type="bullet">
        ///   <item>"Alice" → FirstName = Alice</item>
        ///   <item>"Alice Smith" → FirstName = Alice, LastName = Smith</item>
        ///   <item>"Alice B. Smith" → FirstName = Alice, MiddleName = B., LastName = Smith</item>
        ///   <item>"Alice B. C. Smith" → FirstName = Alice, MiddleName = B., LastName = C. Smith</item>
        /// </list>
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name
        {
            get => null; // write-only: reading always returns null
            set => ParseName(value);
        }

        public string? FirstName { get; set; }

        public string? MiddleName { get; set; }

        public string? LastName { get; set; }

        public string? Company { get; set; }

        public string? Subject { get; set; }

        [JsonConverter(typeof(FlexibleStringDictionaryJsonConverter))]
        public Dictionary<string, string> ExtraData { get; set; } = new();

        [Required]
        public string Message { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email
        {
            get
            {
                return email;
            }

            set
            {
                email = value?.ToLower();
            }
        }

        public string? Phone { get; set; }

        public string RecaptchaToken { get; set; } = string.Empty;

        private void ParseName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(FirstName))
            {
                FirstName = parts[0];
            }

            if (parts.Length == 2 && string.IsNullOrWhiteSpace(LastName))
            {
                LastName = parts[1];
            }
            else if (parts.Length >= 3)
            {
                if (string.IsNullOrWhiteSpace(MiddleName))
                {
                    MiddleName = parts[1];
                }

                if (string.IsNullOrWhiteSpace(LastName))
                {
                    LastName = string.Join(' ', parts.Skip(2));
                }
            }
        }
    }
}