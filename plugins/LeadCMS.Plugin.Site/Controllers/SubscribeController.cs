// <copyright file="SubscribeController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;
using LeadCMS.Exceptions;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;
using LeadCMS.Plugin.Site.Configuration;
using LeadCMS.Plugin.Site.Data;
using LeadCMS.Plugin.Site.DTOs;
using LeadCMS.Plugin.Site.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Plugin.Site.Controllers;

[AllowAnonymous]
public class SubscribesController : Controller
{
    protected static readonly string DefaultGroup = "SubscriberNewsletters";

    protected readonly LeadCmsSiteDbContext dbContext;
    protected readonly IContactService contactService;
    protected readonly IHttpContextHelper httpContextHelper;
    protected readonly IEmailFromTemplateService emailService;
    protected readonly ISubscriptionTokenService tokenService;
    protected readonly ISitePluginSettingsAccessor siteSettingsAccessor;

    public SubscribesController(
        LeadCmsSiteDbContext dbContext,
        IContactService contactService,
        IHttpContextHelper httpContextHelper,
        IEmailFromTemplateService emailService,
        ISubscriptionTokenService tokenService,
        ISitePluginSettingsAccessor siteSettingsAccessor)
    {
        this.dbContext = dbContext;
        this.contactService = contactService;
        this.httpContextHelper = httpContextHelper;
        this.emailService = emailService;
        this.tokenService = tokenService;
        this.siteSettingsAccessor = siteSettingsAccessor;
        this.contactService.SetDBContext(dbContext);
    }

    [HttpPost("api/subscribe")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public virtual async Task<ActionResult> Subscribe([FromBody] SubscribeDto subscribeDto)
    {
        var group = string.IsNullOrWhiteSpace(subscribeDto.Group) ? DefaultGroup : subscribeDto.Group;

        var token = tokenService.Generate(
            subscribeDto.Email,
            group,
            subscribeDto.Language,
            subscribeDto.TimeZoneOffset);

        var confirmationUrl = BuildConfirmationUrl(token);

        await emailService.SendAsync(
            "Subscription_Email_Confirmation",
            subscribeDto.Language,
            new[] { subscribeDto.Email },
            new Dictionary<string, object>
            {
                { "email", subscribeDto.Email },
                { "confirmationUrl", confirmationUrl },
            },
            null);

        return Ok();
    }

    [HttpPost("api/subscribe/confirm")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public virtual async Task<ActionResult> ConfirmSubscription([FromBody] ConfirmSubscribeDto confirmDto)
    {
        var payload = tokenService.Validate(confirmDto.Token);
        if (payload == null)
        {
            return BadRequest("Invalid or expired confirmation token.");
        }

        var contact = await contactService.FindOrCreate(payload.Email, payload.Language, payload.TimeZoneOffset);

        ContactMergeHelper.ApplyPublicUpdate(
            contact,
            nameof(contact.Source),
            contact.Source,
            "Subscribed",
            v => contact.Source = v,
            "Subscribe",
            httpContextHelper?.IpAddress,
            httpContextHelper?.UserAgent);

        await contactService.Subscribe(contact, payload.Group);

        await dbContext.SaveChangesAsync();

        if (string.IsNullOrWhiteSpace(contact.Email))
        {
            return BadRequest("Contact has no email address for subscription confirmation.");
        }

        await emailService.SendAsync(
            "Subscription_Confirmation",
            payload.Language,
            new[] { contact.Email },
            new Dictionary<string, object> { { "email", contact.Email } },
            null);

        return Ok();
    }

    [HttpPost("api/unsubscribe")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public virtual async Task<ActionResult> Unsubscribe([FromBody] UnsibscribeDto subscribeDto)
    {
        var contact = await FindOrThrowNotFound(subscribeDto.Email);

        if (string.IsNullOrWhiteSpace(contact.Email))
        {
            return BadRequest("Contact has no email address for unsubscription.");
        }

        await contactService.Unsubscribe(contact.Email, "Unsubscribed from email or site", "Site", DateTime.UtcNow, httpContextHelper.IpAddress);

        await dbContext.SaveChangesAsync();

        return Ok();
    }

    protected virtual async Task<Contact> FindOrThrowNotFound(string email)
    {
        var existingEntity = await (from p in dbContext.Contacts
                                    where p.Email == email
                                    select p).FirstOrDefaultAsync();

        if (existingEntity == null)
        {
            throw new EntityNotFoundException(typeof(Contact).Name, email);
        }

        return existingEntity;
    }

    protected virtual string BuildConfirmationUrl(string token)
    {
        var settings = siteSettingsAccessor.Settings;
        return settings.ConfirmationUrlTemplate
            .Replace("{siteUrl}", settings.SiteUrl.TrimEnd('/'))
            .Replace("{token}", Uri.EscapeDataString(token));
    }
}