// <copyright file="EmailTemplateService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.Json;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Enums;
using LeadCMS.Geography;
using LeadCMS.Interfaces;
using Microsoft.EntityFrameworkCore;

using static LeadCMS.Helpers.TemplateArgumentsBuilder;

namespace LeadCMS.Services;

public class EmailTemplateService : IEmailTemplateService
{
    private readonly PgDbContext dbContext;
    private readonly ILiquidTemplateService liquidTemplateService;
    private readonly IEmailService emailService;

    public EmailTemplateService(
        PgDbContext dbContext,
        ILiquidTemplateService liquidTemplateService,
        IEmailService emailService)
    {
        this.dbContext = dbContext;
        this.liquidTemplateService = liquidTemplateService;
        this.emailService = emailService;
    }

    /// <inheritdoc/>
    public async Task<EmailTemplatePreviewResultDto> PreviewAsync(EmailTemplatePreviewRequestDto dto)
    {
        Contact? previewContact = null;
        if (dto.ContactId.HasValue)
        {
            previewContact = await LoadPreviewContactAsync(dto.ContactId.Value)
                ?? throw new KeyNotFoundException($"Contact with id {dto.ContactId.Value} not found.");
        }

        var contactType = dto.ContactType ?? PreviewContactType.Full;
        var templateArgs = previewContact != null
            ? FromContact(previewContact)
            : FromContact(BuildDummyContact(contactType), includeNestedObjects: contactType == PreviewContactType.Full);

        var customTemplateArgs = ConvertCustomTemplateParameters(dto.CustomTemplateParameters);
        templateArgs = Merge(templateArgs, customTemplateArgs);

        var bodySource = dto.BodyTemplate;

        var renderedBody = await liquidTemplateService.RenderAsync(bodySource, templateArgs);
        var renderedSubject = await liquidTemplateService.RenderAsync(dto.Subject, templateArgs);

        return new EmailTemplatePreviewResultDto
        {
            RenderedSubject = renderedSubject,
            RenderedBody = renderedBody,
            FromEmail = dto.FromEmail,
            FromName = dto.FromName,
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
            contact = await LoadPreviewContactAsync(dto.ContactId.Value)
                ?? throw new KeyNotFoundException($"Contact with id {dto.ContactId.Value} not found.");
        }

        var sendContactType = dto.ContactType ?? PreviewContactType.Full;
        var templateArgs = contact != null
            ? FromContact(contact)
            : FromContact(BuildDummyContact(sendContactType), includeNestedObjects: sendContactType == PreviewContactType.Full);

        var customTemplateArgs = ConvertCustomTemplateParameters(dto.CustomTemplateParameters);
        templateArgs = Merge(templateArgs, customTemplateArgs);

        var bodySource = dto.BodyTemplate;

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

    /// <summary>
    /// Builds a dummy <see cref="Contact"/> entity populated to the specified detail level.
    /// The entity is never persisted — it is used only with
    /// <c>TemplateArgumentsBuilder.FromContact</c>
    /// to ensure both real and dummy contacts produce template arguments through the same code path.
    /// </summary>
    /// <param name="contactType">The level of detail to populate on the dummy contact.</param>
    /// <returns>A <see cref="Contact"/> entity with sample data matching the requested detail level.</returns>
    internal static Contact BuildDummyContact(PreviewContactType contactType)
    {
        var contact = new Contact { Email = "jane.doe@example.com" };

        if (contactType == PreviewContactType.Minimal)
        {
            return contact;
        }

        // Basic and above — add name fields
        contact.FirstName = "Jane";
        contact.LastName = "Doe";

        if (contactType == PreviewContactType.Basic)
        {
            return contact;
        }

        // Standard and above — add all scalar contact fields
        contact.Prefix = "Ms.";
        contact.Phone = "+1-555-0123";
        contact.JobTitle = "Marketing Manager";
        contact.CompanyName = "Acme Corp";
        contact.Department = "Marketing";
        contact.CityName = "San Francisco";
        contact.State = "CA";
        contact.Zip = "94105";
        contact.Address1 = "123 Market Street";
        contact.Address2 = "Suite 400";
        contact.Language = "en";
        contact.CountryCode = Country.US;
        contact.ContinentCode = Continent.NA;

        if (contactType == PreviewContactType.Standard)
        {
            // Standard includes Account and Domain as top-level flattened fields
            // by setting the related objects with minimal data.
            contact.Account = new Account
            {
                Name = "Acme Corp",
                SiteUrl = "https://www.acme-corp.com",
            };
            contact.Domain = new Domain
            {
                Name = "acme-corp.com",
            };

            return contact;
        }

        // Full — add nested objects
        contact.Account = new Account
        {
            Name = "Acme Corp",
            SiteUrl = "https://www.acme-corp.com",
            CityName = "San Francisco",
            State = "CA",
            EmployeesRange = "50-200",
        };

        contact.Domain = new Domain
        {
            Name = "acme-corp.com",
            Title = "Acme Corp",
            Url = "https://www.acme-corp.com",
        };

        contact.Orders = new List<Order>
        {
            new Order
            {
                RefNo = "ORD-2025-001",
                OrderNumber = "1001",
                Total = 249.99m,
                Currency = "USD",
                Status = OrderStatus.Paid,
                Quantity = 2,
                OrderItems = new List<OrderItem>
                {
                    new OrderItem
                    {
                        ProductName = "Professional Plan (Annual)",
                        Total = 199.99m,
                        Quantity = 1,
                    },
                    new OrderItem
                    {
                        ProductName = "Premium Support Add-on",
                        Total = 50.00m,
                        Quantity = 1,
                    },
                },
            },
        };

        contact.Deals = new List<Deal>
        {
            new Deal
            {
                DealValue = 15000.00m,
                DealCurrency = "USD",
                DealPipeline = new DealPipeline { Name = "Enterprise Sales" },
                DealPipelineStage = new DealPipelineStage { Name = "Proposal Sent" },
            },
        };

        return contact;
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
}
