// <copyright file="SubscribeController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;
using LeadCMS.Exceptions;
using LeadCMS.Interfaces;
using LeadCMS.Plugin.Site.Data;
using LeadCMS.Plugin.Site.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Plugin.Site.Controllers;

[AllowAnonymous]
public class SubscribesController : Controller
{
    private const string OnboardingGuidesGroupName = "Onboarding Guides";

    private readonly LeadCmsSiteDbContext dbContext;
    private readonly IContactService contactService;
    private readonly IHttpContextHelper httpContextHelper;
    private readonly IEmailFromTemplateService emailService;

    public SubscribesController(LeadCmsSiteDbContext dbContext, IContactService contactService, IHttpContextHelper httpContextHelper, IEmailFromTemplateService emailService)
    {
        this.dbContext = dbContext;
        this.contactService = contactService;
        this.httpContextHelper = httpContextHelper;
        this.emailService = emailService;
        this.contactService.SetDBContext(dbContext);
    }

    [HttpPost("api/subscribe")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> Subscribe([FromBody] SubscribeDto subscribeDto)
    {
        var contact = await contactService.FindOrCreate(subscribeDto.Email, subscribeDto.Language, subscribeDto.TimeZoneOffset);

        contact.Source = "Subscribed";

        await contactService.Subscribe(contact, OnboardingGuidesGroupName);

        await dbContext.SaveChangesAsync();
        
        await emailService.SendAsync(
            "Subscription_Confirmation",
            subscribeDto.Language,
            [contact.Email],
            new Dictionary<string, string> { { "email", contact.Email } },
            null);

        return Ok();
    }

    [HttpPost("api/unsubscribe")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> Unsubscribe([FromBody] UnsibscribeDto subscribeDto)
    {
        var contact = await FindOrThrowNotFound(subscribeDto.Email);

        await contactService.Unsubscribe(contact.Email, "Unsubscribed from email or site", "Site", DateTime.UtcNow, httpContextHelper.IpAddress);

        await dbContext.SaveChangesAsync();

        return Ok();
    }

    protected async Task<Contact> FindOrThrowNotFound(string email)
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
}