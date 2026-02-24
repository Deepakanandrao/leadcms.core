// <copyright file="EmailTemplate.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using LeadCMS.DataAnnotations;
using LeadCMS.Enums;
using LeadCMS.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Entities
{
    [Table("email_template")]
    [SupportsChangeLog]
    [Index(nameof(Name), nameof(Language), IsUnique = true)]
    public class EmailTemplate : BaseEntity, ITranslatable
    {
        [Required]
        [Searchable]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Searchable]
        public string Subject { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the HTML template body of the email.
        /// </summary>
        [Required]
        [Searchable]
        public string BodyTemplate { get; set; } = string.Empty;

        [Required]
        [Searchable]
        public string FromEmail { get; set; } = string.Empty;

        [Required]
        [Searchable]
        public string FromName { get; set; } = string.Empty;

        [Required]
        public int EmailGroupId { get; set; }

        [JsonIgnore]
        [ForeignKey("EmailGroupId")]
        public virtual EmailGroup? EmailGroup { get; set; }

        [Required]
        [Searchable]
        public string Language { get; set; } = string.Empty;

        [Searchable]
        public string? TranslationKey { get; set; }

        /// <summary>
        /// Gets or sets the template category that describes its visual style and purpose.
        /// Defaults to General for backward compatibility with existing templates.
        /// </summary>
        public EmailTemplateCategory Category { get; set; } = EmailTemplateCategory.General;

        /// <summary>
        /// Gets or sets how many times an email should resend once sending failed.
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Gets or sets the frequency in minutes where an email should resend after a failed attempt.
        /// </summary>
        public int RetryInterval { get; set; }
    }
}