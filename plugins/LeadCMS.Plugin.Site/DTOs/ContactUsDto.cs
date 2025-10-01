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
    }
}