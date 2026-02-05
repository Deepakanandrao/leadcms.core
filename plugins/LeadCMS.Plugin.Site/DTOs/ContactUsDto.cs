// <copyright file="ContactUsDto.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace LeadCMS.Plugin.Site.DTOs
{
    public class ContactUsDto
    {
        private string email = string.Empty;

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

        [Required]
        public string FirstName { get; set; } = string.Empty;

        public string? LastName { get; set; }

        public string? Company { get; set; }

        public string? Subject { get; set; }

        public Dictionary<string, string> ExtraData { get; set; } = new();

        [Required]
        public string Message { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email
        {
            get
            {
                return email;
            }

            set
            {
                email = value.ToLower();
            }
        }

        [Required]
        public int TimeZoneOffset { get; set; }

        [Required]
        public string Language { get; set; } = string.Empty;

        public string RecaptchaToken { get; set; } = string.Empty;
    }
}