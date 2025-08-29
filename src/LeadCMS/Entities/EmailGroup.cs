// <copyright file="EmailGroup.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using LeadCMS.DataAnnotations;
using LeadCMS.Interfaces;

namespace LeadCMS.Entities
{
    [Table("email_group")]
    [SupportsChangeLog]
    public class EmailGroup : BaseEntity, ITranslatable
    {
        /// <summary>
        /// Gets or sets the name of the email group.
        /// </summary>
        [Required]
        [Searchable]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Searchable]
        public string Language { get; set; } = string.Empty;

        [Searchable]
        public string? TranslationKey { get; set; }

        [JsonIgnore]
        public virtual ICollection<EmailTemplate>? EmailTemplates { get; set; }
    }
}