// <copyright file="Contact.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using LeadCMS.DataAnnotations;
using LeadCMS.Geography;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Entities;

[Table("contact")]
[SupportsElastic]
[SupportsChangeLog]
[SurrogateIdentity(nameof(Email))]
public class Contact : BaseEntity, ICommentable
{
    private string? email;

    [Searchable]
    public string? Prefix { get; set; }

    [Searchable]
    public string? FirstName { get; set; }

    [Searchable]
    public string? MiddleName { get; set; }

    [Searchable]
    public string? LastName { get; set; }

    [Searchable]
    public string? FullName { get; private set; }

    [Searchable]
    public DateTime? Birthday { get; set; }

    [Searchable]
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

    [Searchable]
    public Continent? ContinentCode { get; set; }

    [Searchable]
    public Country? CountryCode { get; set; }

    [Searchable]
    public string? CityName { get; set; }

    [Searchable]
    public string? Address1 { get; set; }

    [Searchable]
    public string? Address2 { get; set; }

    [Searchable]
    public string? JobTitle { get; set; }

    [Searchable]
    public string? CompanyName { get; set; }

    [Searchable]
    public string? Department { get; set; }

    [Searchable]
    public string? State { get; set; }

    [Searchable]
    public string? Zip { get; set; }

    [Searchable]
    public string? Phone { get; set; }

    public string? PhoneRaw { get; set; }

    public int? Timezone { get; set; }

    [Searchable]
    public string? Language { get; set; }

    [Searchable]
    public string? TranslationKey { get; set; }

    [Searchable]
    [Column(TypeName = "jsonb")]
    public Dictionary<string, string>? SocialMedia { get; set; }

    [Searchable]
    [Column(TypeName = "jsonb")]
    public string[]? Tags { get; set; }

    public int DealsCount { get; set; }

    public int OrdersCount { get; set; }

    public DateTime? LastOrderDate { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal TotalRevenue { get; set; }

    [Column(TypeName = "jsonb")]
    public List<PendingContactUpdate>? PendingUpdates { get; set; }

    public int? DomainId { get; set; }

    [JsonIgnore]
    [ForeignKey("DomainId")]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public virtual Domain? Domain { get; set; }

    public int? AccountId { get; set; }

    [JsonIgnore]
    [ForeignKey("AccountId")]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public virtual Account? Account { get; set; }

    public int? UnsubscribeId { get; set; }

    [JsonIgnore]
    [ForeignKey("UnsubscribeId")]
    [DeleteBehavior(DeleteBehavior.SetNull)]
    public virtual Unsubscribe? Unsubscribe { get; set; }

    [JsonIgnore]
    public virtual ICollection<Order>? Orders { get; set; }

    [JsonIgnore]
    public virtual ICollection<Deal>? Deals { get; set; }

    public static string GetCommentableType()
    {
        return "Contact";
    }
}