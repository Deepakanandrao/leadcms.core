// <copyright file="EmailTemplateService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.Json;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Enums;
using LeadCMS.Interfaces;
using Microsoft.EntityFrameworkCore;

using static LeadCMS.Helpers.TemplateArgumentsBuilder;

namespace LeadCMS.Services;

public class EmailTemplateService : IEmailTemplateService
{
    private readonly PgDbContext dbContext;
    private readonly ILiquidTemplateService liquidTemplateService;
    private readonly IMjmlRenderingService mjmlRenderingService;
    private readonly IEmailService emailService;

    public EmailTemplateService(
        PgDbContext dbContext,
        ILiquidTemplateService liquidTemplateService,
        IMjmlRenderingService mjmlRenderingService,
        IEmailService emailService)
    {
        this.dbContext = dbContext;
        this.liquidTemplateService = liquidTemplateService;
        this.mjmlRenderingService = mjmlRenderingService;
        this.emailService = emailService;
    }

    /// <inheritdoc/>
    public async Task<EmailTemplatePreviewResultDto> PreviewAsync(EmailTemplatePreviewRequestDto dto)
    {
        var template = await dbContext.EmailTemplates!.FindAsync(dto.EmailTemplateId)
            ?? throw new KeyNotFoundException($"Email template with id {dto.EmailTemplateId} not found.");

        Contact? previewContact = null;
        if (dto.ContactId.HasValue)
        {
            previewContact = await LoadPreviewContactAsync(dto.ContactId.Value)
                ?? throw new KeyNotFoundException($"Contact with id {dto.ContactId.Value} not found.");
        }

        var templateArgs = previewContact != null
            ? FromContact(previewContact)
            : BuildDummyContactArgs(dto.ContactType ?? PreviewContactType.Full);

        var customTemplateArgs = ConvertCustomTemplateParameters(dto.CustomTemplateParameters);
        templateArgs = Merge(templateArgs, customTemplateArgs);

        var bodySource = template.Format == EmailTemplateFormat.Mjml
            ? mjmlRenderingService.RenderToHtml(template.BodyTemplate)
            : template.BodyTemplate;

        var renderedBody = await liquidTemplateService.RenderAsync(bodySource, templateArgs);
        var renderedSubject = await liquidTemplateService.RenderAsync(template.Subject, templateArgs);

        return new EmailTemplatePreviewResultDto
        {
            RenderedSubject = renderedSubject,
            RenderedBody = renderedBody,
            FromEmail = template.FromEmail,
            FromName = template.FromName,
            PreviewContactId = previewContact?.Id ?? 0,
            PreviewContactName = previewContact?.FullName ?? (string)templateArgs["FullName"],
            PreviewContactEmail = previewContact?.Email ?? (string)templateArgs["Email"],
        };
    }

    /// <inheritdoc/>
    public async Task SendTestEmailAsync(EmailTemplateSendTestDto dto)
    {
        Contact? contact = null;
        if (dto.ContactId.HasValue)
        {
            contact = await LoadContactWithBasicRelationsAsync(dto.ContactId.Value)
                ?? throw new KeyNotFoundException($"Contact with id {dto.ContactId.Value} not found.");
        }

        var templateArgs = contact != null
            ? FromContact(contact)
            : BuildDummyContactArgs(dto.ContactType ?? PreviewContactType.Full);

        var customTemplateArgs = ConvertCustomTemplateParameters(dto.CustomTemplateParameters);
        templateArgs = Merge(templateArgs, customTemplateArgs);

        var bodySource = dto.Format == EmailTemplateFormat.Mjml
            ? mjmlRenderingService.RenderToHtml(dto.BodyTemplate)
            : dto.BodyTemplate;

        var renderedBody = await liquidTemplateService.RenderAsync(bodySource, templateArgs);
        var renderedSubject = await liquidTemplateService.RenderAsync(dto.Subject, templateArgs);

        await emailService.SendAsync(
            renderedSubject,
            dto.FromEmail,
            dto.FromName,
            new[] { dto.RecipientEmail },
            renderedBody,
            attachments: null);
    }

    internal static Dictionary<string, object>? ConvertCustomTemplateParameters(Dictionary<string, JsonElement>? customTemplateParameters)
    {
        if (customTemplateParameters == null || customTemplateParameters.Count == 0)
        {
            return null;
        }

        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in customTemplateParameters)
        {
            var converted = ConvertJsonElement(value);
            if (converted != null)
            {
                result[key] = converted;
            }
        }

        return result;
    }

    internal static object? ConvertJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var longValue))
                {
                    return longValue;
                }

                if (element.TryGetDecimal(out var decimalValue))
                {
                    return decimalValue;
                }

                return element.GetDouble();
            case JsonValueKind.True:
            case JsonValueKind.False:
                return element.GetBoolean();
            case JsonValueKind.Object:
                var obj = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in element.EnumerateObject())
                {
                    var value = ConvertJsonElement(property.Value);
                    if (value != null)
                    {
                        obj[property.Name] = value;
                    }
                }

                return obj;
            case JsonValueKind.Array:
                return element.EnumerateArray()
                    .Select(ConvertJsonElement)
                    .Where(value => value != null)
                    .Cast<object>()
                    .ToList();
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            default:
                return null;
        }
    }

    private static Dictionary<string, object> BuildDummyContactArgs(PreviewContactType contactType)
    {
        return contactType switch
        {
            PreviewContactType.Full => BuildFullDummyContactArgs(),
            PreviewContactType.Standard => BuildStandardDummyContactArgs(),
            PreviewContactType.Basic => BuildBasicDummyContactArgs(),
            PreviewContactType.Minimal => BuildMinimalDummyContactArgs(),
            _ => BuildFullDummyContactArgs(),
        };
    }

    private static Dictionary<string, object> BuildMinimalDummyContactArgs()
    {
        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Email"] = "jane.doe@example.com",
            ["FirstName"] = string.Empty,
            ["LastName"] = string.Empty,
            ["FullName"] = string.Empty,
        };
    }

    private static Dictionary<string, object> BuildBasicDummyContactArgs()
    {
        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Email"] = "jane.doe@example.com",
            ["FirstName"] = "Jane",
            ["LastName"] = "Doe",
            ["FullName"] = "Jane Doe",
        };
    }

    private static Dictionary<string, object> BuildStandardDummyContactArgs()
    {
        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Email"] = "jane.doe@example.com",
            ["FirstName"] = "Jane",
            ["LastName"] = "Doe",
            ["FullName"] = "Jane Doe",
            ["MiddleName"] = string.Empty,
            ["Prefix"] = "Ms.",
            ["Phone"] = "+1-555-0123",
            ["JobTitle"] = "Marketing Manager",
            ["CompanyName"] = "Acme Corp",
            ["Department"] = "Marketing",
            ["CityName"] = "San Francisco",
            ["State"] = "CA",
            ["Zip"] = "94105",
            ["Address1"] = "123 Market Street",
            ["Address2"] = "Suite 400",
            ["Language"] = "en",
            ["CountryCode"] = "US",
            ["ContinentCode"] = "NA",
            ["AccountName"] = "Acme Corp",
            ["AccountSiteUrl"] = "https://www.acme-corp.com",
            ["DomainName"] = "acme-corp.com",
        };
    }

    private static Dictionary<string, object> BuildFullDummyContactArgs()
    {
        var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Email"] = "jane.doe@example.com",
            ["FirstName"] = "Jane",
            ["LastName"] = "Doe",
            ["FullName"] = "Jane Doe",
            ["MiddleName"] = string.Empty,
            ["Prefix"] = "Ms.",
            ["Phone"] = "+1-555-0123",
            ["JobTitle"] = "Marketing Manager",
            ["CompanyName"] = "Acme Corp",
            ["Department"] = "Marketing",
            ["CityName"] = "San Francisco",
            ["State"] = "CA",
            ["Zip"] = "94105",
            ["Address1"] = "123 Market Street",
            ["Address2"] = "Suite 400",
            ["Language"] = "en",
            ["CountryCode"] = "US",
            ["ContinentCode"] = "NA",
            ["AccountName"] = "Acme Corp",
            ["AccountSiteUrl"] = "https://www.acme-corp.com",
            ["Account"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Name"] = "Acme Corp",
                ["SiteUrl"] = "https://www.acme-corp.com",
                ["CityName"] = "San Francisco",
                ["State"] = "CA",
                ["EmployeesRange"] = "50-200",
            },
            ["DomainName"] = "acme-corp.com",
            ["Domain"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Name"] = "acme-corp.com",
                ["Title"] = "Acme Corp",
                ["Url"] = "https://www.acme-corp.com",
            },
            ["Orders"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["RefNo"] = "ORD-2025-001",
                    ["OrderNumber"] = "1001",
                    ["Total"] = 249.99m,
                    ["Currency"] = "USD",
                    ["Status"] = "Completed",
                    ["Quantity"] = 2,
                    ["OrderItems"] = new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["ProductName"] = "Professional Plan (Annual)",
                            ["Total"] = 199.99m,
                            ["Quantity"] = 1,
                        },
                        new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["ProductName"] = "Premium Support Add-on",
                            ["Total"] = 50.00m,
                            ["Quantity"] = 1,
                        },
                    },
                },
            },
            ["Deals"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DealValue"] = 15000.00m,
                    ["DealCurrency"] = "USD",
                    ["DealPipeline"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Name"] = "Enterprise Sales",
                    },
                    ["DealPipelineStage"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Name"] = "Proposal Sent",
                    },
                },
            },
        };

        return args;
    }

    private async Task<Contact?> LoadPreviewContactAsync(int contactId)
    {
        return await dbContext.Contacts!
            .Include(c => c.Account)
            .Include(c => c.Domain)
            .Include(c => c.Orders)!
                .ThenInclude(o => o.OrderItems)
            .Include(c => c.Deals)!
                .ThenInclude(d => d.DealPipeline)
            .Include(c => c.Deals)!
                .ThenInclude(d => d.DealPipelineStage)
            .FirstOrDefaultAsync(c => c.Id == contactId);
    }

    private async Task<Contact?> LoadContactWithBasicRelationsAsync(int contactId)
    {
        return await dbContext.Contacts!
            .Include(c => c.Account)
            .Include(c => c.Domain)
            .FirstOrDefaultAsync(c => c.Id == contactId);
    }
}
