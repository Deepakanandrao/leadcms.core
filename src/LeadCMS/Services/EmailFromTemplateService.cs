// <copyright file="EmailFromTemplateService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Configuration;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LeadCMS.Services
{
    public class EmailFromTemplateService : IEmailFromTemplateService
    {
        private readonly Dictionary<string, EmailTemplate> hardcodedTemplates;

        private readonly IEmailWithLogService emailWithLogService;
        private readonly PgDbContext pgDbContext;
        private readonly IConfiguration configuration;
        private readonly ILiquidTemplateService liquidTemplateService;

        public EmailFromTemplateService(IEmailWithLogService emailWithLogService, PgDbContext pgDbContext, IOptions<ApiSettingsConfig> apiSettingsConfig, IConfiguration configuration, ILiquidTemplateService liquidTemplateService)
        {
            this.emailWithLogService = emailWithLogService;
            this.pgDbContext = pgDbContext;
            this.configuration = configuration;
            this.liquidTemplateService = liquidTemplateService;

            var defaultFromEmail = apiSettingsConfig.Value.DefaultFromEmail;
            var defaultFromName = apiSettingsConfig.Value.DefaultFromName;

            // Initialize hardcoded templates with configuration values
            hardcodedTemplates = new Dictionary<string, EmailTemplate>
            {
                ["Password_Reset"] = new EmailTemplate
                {
                    Name = "Password_Reset",
                    Subject = "Password Reset",
                    BodyTemplate = "Click <a href=\"${ResetUrl}\">here</a> to reset your password.",
                    FromEmail = defaultFromEmail,
                    FromName = defaultFromName,
                },

                ["Account_Created"] = new EmailTemplate
                {
                    Name = "Account_Created",
                    Subject = "Your account has been created",
                    BodyTemplate = "Hello ${UserName},<br/>Your account has been created. Your password is: <b>${Password}</b>",
                    FromEmail = defaultFromEmail,
                    FromName = defaultFromName,
                },

                ["Password_Updated"] = new EmailTemplate
                {
                    Name = "Password_Updated",
                    Subject = "Your password has been updated",
                    BodyTemplate = "Hello ${UserName},<br/>Your password has been updated. Your new password is: <b>${Password}</b>",
                    FromEmail = defaultFromEmail,
                    FromName = defaultFromName,
                },
            };
        }

        public async Task SendAsync(string templateName, string language, string[] recipients, Dictionary<string, object>? templateArguments, List<AttachmentDto>? attachments, int contactId = 0, int campaignId = 0)
        {
            var template = await GetEmailTemplateByLanguageOrHardcoded(templateName, language);

            var bodySource = template.BodyTemplate;

            // Step 2: render Liquid (normalises legacy placeholders, evaluates expressions)
            var body = await liquidTemplateService.RenderAsync(bodySource, templateArguments);
            var subject = await liquidTemplateService.RenderAsync(template.Subject, templateArguments);

            await emailWithLogService.SendAsync(subject, template.FromEmail, template.FromName, recipients, body, attachments, template.Id, contactId, campaignId);
        }

        public async Task SendToContactAsync(int contactId, string templateName, Dictionary<string, object>? templateArguments, List<AttachmentDto>? attachments, int scheduleId = 0, int campaignId = 0)
        {
            var template = await GetEmailTemplate(templateName, contactId);

            var bodySource = template.BodyTemplate;

            // Step 2: render Liquid (normalises legacy placeholders, evaluates expressions)
            var body = await liquidTemplateService.RenderAsync(bodySource, templateArguments);
            var subject = await liquidTemplateService.RenderAsync(template.Subject, templateArguments);

            await emailWithLogService.SendToContactAsync(contactId, subject, template.FromEmail, template.FromName, body, attachments, scheduleId, template.Id, campaignId);
        }

        private async Task<EmailTemplate> GetEmailTemplate(string name, int contactId)
        {
            var contact = await pgDbContext.Contacts!.FirstOrDefaultAsync(c => c.Id == contactId)
                ?? throw new EntityNotFoundException(nameof(Contact), contactId.ToString());

            var language = contact.Language;

            var template = await GetEmailTemplateByLanguage(name, language);

            if (template == null)
            {
                throw new UnprocessableEntityException($"Invalid template name '{name}'. Email template was not found.");
            }

            return template;
        }

        private async Task<EmailTemplate?> GetEmailTemplateByLanguage(string name, string? language)
        {
            string defaultLang = LanguageHelper.GetDefaultLanguage(configuration);

            // set default if not set
            language ??= defaultLang;

            if (language.Length == 2)
            {
                var twoLetterBasedLangMatch = await pgDbContext.EmailTemplates!
                    .Where(x => x.Name == name && x.Language.StartsWith(language))
                    .OrderBy(x => x.Language)
                    .FirstOrDefaultAsync();

                if (twoLetterBasedLangMatch != null)
                {
                    return twoLetterBasedLangMatch;
                }
            }

            // try to find template by provided language
            var template = await pgDbContext.EmailTemplates!.FirstOrDefaultAsync(x => x.Name == name && x.Language == language);

            // if template not found, try find with default language
            template ??= await pgDbContext.EmailTemplates!.FirstOrDefaultAsync(x => x.Name == name && x.Language == defaultLang);

            return template;
        }

        private async Task<EmailTemplate> GetEmailTemplateByLanguageOrHardcoded(string name, string? language)
        {
            var template = await GetEmailTemplateByLanguage(name, language);
            if (template != null)
            {
                return template;
            }

            // Try hardcoded
            if (hardcodedTemplates.TryGetValue(name, out var hardcoded))
            {
                return hardcoded;
            }

            throw new InvalidOperationException($"No email template found for '{name}' and language '{language}'.");
        }
    }
}