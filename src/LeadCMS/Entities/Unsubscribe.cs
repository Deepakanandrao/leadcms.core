// <copyright file="Unsubscribe.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using LeadCMS.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Entities;

[Table("unsubscribe")]
[SupportsChangeLog]
[SupportsElastic]
public class Unsubscribe : BaseCreateByEntity
{
    [Searchable]
    public string Reason { get; set; } = string.Empty;

    public int? ContactId { get; set; }

    [JsonIgnore]
    [ForeignKey("ContactId")]
    [DeleteBehavior(DeleteBehavior.SetNull)]
    public virtual Contact? Contact { get; set; }
}
