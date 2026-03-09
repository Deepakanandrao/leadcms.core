// <copyright file="SubscribeDto.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace LeadCMS.Plugin.Site.DTOs;

public class SiteSubscribeDto : ClientLocaleAwareDto
{
    private string email = string.Empty;

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
            email = value?.ToLower() ?? string.Empty;
        }
    }

    public string Group { get; set; } = "SubscriberNewsletters";
}

public class SiteUnsubscribeDto : ClientLocaleAwareDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}