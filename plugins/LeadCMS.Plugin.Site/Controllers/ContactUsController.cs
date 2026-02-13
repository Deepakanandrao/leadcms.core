// <copyright file="ContactUsController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using LeadCMS.DTOs;
using LeadCMS.Interfaces;
using LeadCMS.Plugin.Site.Configuration;
using LeadCMS.Plugin.Site.Data;
using LeadCMS.Plugin.Site.DTOs;
using LeadCMS.Plugin.Site.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace LeadCMS.Plugin.Site.Controllers;

[AllowAnonymous]
[Route("api/contact-us")]
public class ContactUsController : Controller
{
    private readonly IEmailFromTemplateService emailService;
    private readonly PluginSettings? pluginSettings;
    private readonly LeadCmsSiteDbContext dbContext;
    private readonly IContactService contactService;
    private readonly ILeadNotificationService leadNotificationService;
    private readonly IHttpContextHelper? httpContextHelper;

    public ContactUsController(
        IEmailFromTemplateService emailService,
        IConfiguration configuration,
        LeadCmsSiteDbContext dbContext,
        IContactService contactService,
        ILeadNotificationService leadNotificationService,
        IHttpContextHelper httpContextHelper)
    {
        this.emailService = emailService;
        this.dbContext = dbContext;
        this.contactService = contactService;
        this.contactService.SetDBContext(dbContext);
        this.leadNotificationService = leadNotificationService;
        this.httpContextHelper = httpContextHelper;
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
        if (contactUsDto.ExtraData.Count == 0 && Request.HasFormContentType && Request.Form.TryGetValue("ExtraData", out var extraDataValue))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(extraDataValue.ToString());
                if (parsed != null)
                {
                    contactUsDto.ExtraData = parsed;
                }
            }
            catch (JsonException)
            {
                // Ignore invalid JSON in ExtraData form field
            }
        }

        if (!string.IsNullOrWhiteSpace(pluginSettings?.RecaptchaSecretKey) && pluginSettings.RecaptchaSecretKey != "$RECAPTCHA_SECRET_KEY")
        {
            if (string.IsNullOrWhiteSpace(contactUsDto.RecaptchaToken))
            {
                return BadRequest("Missing reCAPTCHA token.");
            }

            using var client = new HttpClient();
            var postData = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("secret", pluginSettings.RecaptchaSecretKey),
                new KeyValuePair<string, string>("response", contactUsDto.RecaptchaToken),
                new KeyValuePair<string, string>("remoteip", httpContextHelper?.IpAddress ?? string.Empty),
            ]);
            var response = await client.PostAsync("https://www.google.com/recaptcha/api/siteverify", postData);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Error verifying reCAPTCHA.");
            }

            var json = await response.Content.ReadAsStringAsync();

            var recaptchaResult = JsonSerializer.Deserialize<RecaptchaVerifyResponse>(json);
            if (recaptchaResult == null || !recaptchaResult.Success)
            {
                return BadRequest("Failed reCAPTCHA validation.");
            }
        }

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

        contact.Source = string.IsNullOrWhiteSpace(contactUsDto.Title)
                ? "Contact Us"
                : contactUsDto.Title;

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

        // Build lead notification info
        var leadInfo = new LeadNotificationInfo
        {
            Title = string.IsNullOrWhiteSpace(contactUsDto.Title)
                ? "New contact form submission"
                : contactUsDto.Title,
            NotificationType = contactUsDto.NotificationType,
            FirstName = contactUsDto.FirstName,
            LastName = contactUsDto.LastName,
            Email = contactUsDto.Email,
            Company = contactUsDto.Company,
            PageUrl = contactUsDto.PageUrl,
            Subject = contactUsDto.Subject,
            Message = contactUsDto.Message,
            Language = contactUsDto.Language,
            ExtraData = contactUsDto.ExtraData,
            Attachments = attachmentFiles.Count > 0 ? attachmentFiles : null,
            TimeZoneOffset = contactUsDto.TimeZoneOffset,
            IpAddress = httpContextHelper?.IpAddress,
            UserAgent = httpContextHelper?.UserAgent,
            ContactId = contact.Id,
        };

        // Send lead notifications to all enabled channels (email, Telegram, Slack)
        await leadNotificationService.SendLeadNotificationAsync(leadInfo);

        // Send acknowledgment to the user only if the email is present and valid
        if (!string.IsNullOrWhiteSpace(contactUsDto.Email) && MailboxAddress.TryParse(contactUsDto.Email, out _))
        {
            var acknowledgmentTemplate = string.IsNullOrWhiteSpace(contactUsDto.AcknowledgmentType)
                ? "Acknowledgment"
                : contactUsDto.AcknowledgmentType;

            // Use same template arguments as notification email
            var templateArgs = LeadNotificationService.BuildEmailTemplateArguments(leadInfo);

            await emailService.SendToContactAsync(
                contact.Id,
                acknowledgmentTemplate,
                templateArgs,
                null);
        }

        return Ok(contactUsDto);
    }
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

public class RecaptchaVerifyResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("challenge_ts")]
    public DateTime ChallengeTs { get; set; }

    [JsonPropertyName("hostname")]
    public string Hostname { get; set; } = string.Empty;

    [JsonPropertyName("error-codes")]
    public string[] ErrorCodes { get; set; } = Array.Empty<string>();
}