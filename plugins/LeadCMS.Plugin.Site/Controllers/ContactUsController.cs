// <copyright file="ContactUsController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>
using System.Diagnostics.CodeAnalysis;
using LeadCMS.DTOs;
using LeadCMS.Interfaces;
using LeadCMS.Plugin.Site.Configuration;
using LeadCMS.Plugin.Site.Data;
using LeadCMS.Plugin.Site.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace LeadCMS.Plugin.Site.Controllers;

[AllowAnonymous]
[Route("api/contact-us")]
public class ContactUsController : Controller
{
    private readonly IEmailFromTemplateService emailService;
    private readonly PluginSettings? pluginSettings;
    private readonly LeadCmsSiteDbContext dbContext;
    private readonly IContactService contactService;

    public ContactUsController(
        IEmailFromTemplateService emailService,
        IConfiguration configuration,
        LeadCmsSiteDbContext dbContext,
        IContactService contactService)
    {
        this.emailService = emailService;
        this.dbContext = dbContext;
        this.contactService = contactService;
        this.contactService.SetDBContext(dbContext);

        var settings = configuration.Get<PluginSettings>();

        if (settings != null)
        {
            pluginSettings = settings;
        }
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> Post([FromForm] ContactUsDto contactUsDto)
    {
        // Create or find contact record
        var contact = await contactService.FindOrCreate(contactUsDto.Email, contactUsDto.Language, contactUsDto.TimeZoneOffset);

        // Populate contact attributes from the request
        if (!string.IsNullOrWhiteSpace(contactUsDto.FirstName))
        {
            contact.FirstName = contactUsDto.FirstName;
        }

        if (!string.IsNullOrWhiteSpace(contactUsDto.LastName))
        {
            contact.LastName = contactUsDto.LastName;
        }

        if (!string.IsNullOrWhiteSpace(contactUsDto.Company))
        {
            contact.CompanyName = contactUsDto.Company;
        }

        contact.Source = "Contact Us";

        var attachmentFiles = new List<AttachmentDto>();

        if (contactUsDto.Attachment != null)
        {
            attachmentFiles.Add(new AttachmentDto
            {
                FileName = contactUsDto.Attachment.FileName,
                File = await contactUsDto.Attachment.GetBytes(),
            });
        }

        // Save contact changes
        await dbContext.SaveChangesAsync();

        await emailService.SendAsync("Contact_Us", contactUsDto.Language, pluginSettings!.ContactUs.To, GetTemplateArguments(contactUsDto), attachmentFiles);

        // Send acknowledgment to the user
        await emailService.SendAsync(
            "Acknowledgment",
            contactUsDto.Language,
            [contactUsDto.Email],
            new Dictionary<string, string> { { "firstName", Encode(contactUsDto.FirstName) } },
            null);

        return Ok(contactUsDto);
    }

    private static Dictionary<string, string> GetTemplateArguments(ContactUsDto contactUsDto)
    {
        Dictionary<string, string> templateArg = new Dictionary<string, string>
        {
            { "fromEmail", Encode(contactUsDto.Email) },
            { "firstName", Encode(contactUsDto.FirstName) },
            { "lastName", Encode(contactUsDto.LastName) ?? string.Empty },
            { "company", Encode(contactUsDto.Company) ?? string.Empty },
            { "subject", Encode(contactUsDto.Subject) ?? string.Empty },
            { "message", Encode(contactUsDto.Message) },
        };

        foreach (var item in contactUsDto.ExtraData)
        {
            templateArg.Add($"extraData[{item.Key}]", Encode(item.Value));
        }

        return templateArg;
    }

    [return: NotNullIfNotNull(nameof(value))]
    private static string? Encode(string? value) => System.Web.HttpUtility.HtmlEncode(value);
}

public static class FormFileExtensions
{
    public static async Task<byte[]> GetBytes(this IFormFile formFile)
    {
        await using var memoryStream = new MemoryStream();
        await formFile.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }
}