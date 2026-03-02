// <copyright file="ContactEmailCommunicationsTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Helpers;
using LeadCMS.Infrastructure;

namespace LeadCMS.Tests;

public class ContactEmailCommunicationsTests : BaseTestAutoLogin
{
    public ContactEmailCommunicationsTests()
    {
        TrackEntityType<Contact>();
        TrackEntityType<EmailLog>();
        TrackEntityType<Domain>();
    }

    [Fact]
    public async Task GetEmailCommunications_ShouldReturnScopedListWithBodyAndTotalCount()
    {
        var contact1 = await CreateContactAsync("mail_scope_1");
        var contact2 = await CreateContactAsync("mail_scope_2");

        var dbContext = App.GetDbContext()!;
        var baseDate = DateTime.UtcNow.Date.AddHours(10);

        await dbContext.EmailLogs!.AddRangeAsync(
        new EmailLog
        {
            ContactId = contact1.Id,
            Subject = "Follow up",
            Recipients = contact1.Email!,
            FromEmail = "sales@leadcms.ai",
            TextBody = "Hi Peter,\nCan we sync this afternoon?\n\nOn Mon, Feb 10, 2026 at 9:00 AM Alice <alice@test.net> wrote:\n> Previous thread",
            MessageId = "scope-1",
            Status = EmailStatus.Sent,
            CreatedAt = baseDate,
        },
        new EmailLog
        {
            ContactId = contact1.Id,
            Subject = "Re: Follow up",
            Recipients = "sales@leadcms.ai",
            FromEmail = contact1.Email!,
            HtmlBody = "<p>Sure, let's do 4 PM.</p>",
            MessageId = "scope-2",
            Status = EmailStatus.Received,
            CreatedAt = baseDate.AddMinutes(30),
        },
        new EmailLog
        {
            ContactId = contact2.Id,
            Subject = "Other contact email",
            Recipients = contact2.Email!,
            FromEmail = "sales@leadcms.ai",
            TextBody = "This should not be in the list",
            MessageId = "scope-3",
            Status = EmailStatus.Sent,
            CreatedAt = baseDate.AddMinutes(40),
        });
        await dbContext.SaveChangesAsync();

        var response = await GetTest($"/api/contacts/{contact1.Id}/email-communications");
        var payload = JsonHelper.Deserialize<List<ContactEmailCommunicationListItemDto>>(await response.Content.ReadAsStringAsync());

        payload.Should().NotBeNull();
        payload!.Count.Should().Be(2);
        payload.Should().OnlyContain(item => item.ContactId == contact1.Id);

        var totalCountHeader = response.Headers.GetValues(ResponseHeaderNames.TotalCount).FirstOrDefault();
        totalCountHeader.Should().Be("2");

        payload[0].CreatedAt.Should().BeAfter(payload[1].CreatedAt);
        payload.Should().OnlyContain(item => !string.IsNullOrWhiteSpace(item.Body));

        var followUpItem = payload.First(item => item.Subject == "Follow up");
        followUpItem.Body.Should().Contain("Can we sync this afternoon?");
        followUpItem.Body.Should().NotContain("On Mon, Feb 10, 2026");

        var replyItem = payload.First(item => item.Subject == "Re: Follow up");
        replyItem.Body.Should().Contain("<p>");
    }

    [Fact]
    public async Task GetEmailCommunication_ShouldStripOutlookBorderSeparatorFromHtmlBody()
    {
        var contact = await CreateContactAsync("mail_outlook_sep");
        var dbContext = App.GetDbContext()!;

        var htmlBody = "<div class=WordSection1><p>Main message content</p><p>&nbsp;</p>"
            + "<div style='border:none;border-top:solid #E1E1E1 1.0pt;padding:3.0pt 0cm 0cm 0cm'>"
            + "<p><b><span>From:</span></b><span> Test User &lt;test@example.com&gt;<br>"
            + "<b>Sent:</b> Monday, January 20, 2026 10:00 AM<br>"
            + "<b>To:</b> support@example.com<br>"
            + "<b>Subject:</b> Original subject</span></p></div>"
            + "<p>This is the quoted reply content</p></div>";

        var log = new EmailLog
        {
            ContactId = contact.Id,
            Subject = "Outlook thread",
            Recipients = contact.Email!,
            FromEmail = "support@example.com",
            HtmlBody = htmlBody,
            MessageId = "outlook-sep-1",
            Status = EmailStatus.Received,
            CreatedAt = DateTime.UtcNow,
        };

        await dbContext.EmailLogs!.AddAsync(log);
        await dbContext.SaveChangesAsync();

        var detail = await GetTest<ContactEmailCommunicationDetailsDto>($"/api/contacts/{contact.Id}/email-communications/{log.Id}");
        detail.Should().NotBeNull();
        detail!.Body.Should().Contain("Main message content");
        detail.Body.Should().NotContain("From:");
        detail.Body.Should().NotContain("quoted reply content");
    }

    [Fact]
    public async Task GetEmailCommunication_ShouldEnforceContactScope()
    {
        var contact1 = await CreateContactAsync("mail_detail_1");
        var contact2 = await CreateContactAsync("mail_detail_2");

        var dbContext = App.GetDbContext()!;
        var log = new EmailLog
        {
            ContactId = contact2.Id,
            Subject = "Scoped detail",
            Recipients = contact2.Email!,
            FromEmail = "hello@leadcms.ai",
            HtmlBody = "<p>Body</p>",
            MessageId = "detail-1",
            Status = EmailStatus.Sent,
            CreatedAt = DateTime.UtcNow,
        };

        await dbContext.EmailLogs!.AddAsync(log);
        await dbContext.SaveChangesAsync();

        await GetTest($"/api/contacts/{contact1.Id}/email-communications/{log.Id}", HttpStatusCode.NotFound);

        var detail = await GetTest<ContactEmailCommunicationDetailsDto>($"/api/contacts/{contact2.Id}/email-communications/{log.Id}");
        detail.Should().NotBeNull();
        detail!.Subject.Should().Be("Scoped detail");
        detail.ContactId.Should().Be(contact2.Id);
        detail.Body.Should().Contain("<p>Body</p>");
    }

    [Fact]
    public async Task GetEmailCommunicationStats_ShouldReturnStatusBreakdownAndTimeline()
    {
        var contact = await CreateContactAsync("mail_stats");
        var dbContext = App.GetDbContext()!;

        await dbContext.EmailLogs!.AddRangeAsync(
        new EmailLog
        {
            ContactId = contact.Id,
            Subject = "S1",
            Recipients = contact.Email!,
            FromEmail = "s@leadcms.ai",
            MessageId = "stats-1",
            Status = EmailStatus.Sent,
            CreatedAt = new DateTime(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc),
        },
        new EmailLog
        {
            ContactId = contact.Id,
            Subject = "R1",
            Recipients = "s@leadcms.ai",
            FromEmail = contact.Email!,
            MessageId = "stats-2",
            Status = EmailStatus.Received,
            CreatedAt = new DateTime(2026, 2, 1, 15, 0, 0, DateTimeKind.Utc),
        },
        new EmailLog
        {
            ContactId = contact.Id,
            Subject = "N1",
            Recipients = contact.Email!,
            FromEmail = "s@leadcms.ai",
            MessageId = "stats-3",
            Status = EmailStatus.NotSent,
            CreatedAt = new DateTime(2026, 2, 2, 10, 0, 0, DateTimeKind.Utc),
        });
        await dbContext.SaveChangesAsync();

        var stats = await GetTest<ContactEmailCommunicationStatsDto>(
            $"/api/contacts/{contact.Id}/email-communications/stats?groupBy=Day&from=2026-02-01T00:00:00Z&to=2026-02-02T23:59:59Z");

        stats.Should().NotBeNull();
        stats!.ContactId.Should().Be(contact.Id);
        stats.TotalCount.Should().Be(3);
        stats.SentCount.Should().Be(1);
        stats.ReceivedCount.Should().Be(1);
        stats.NotSentCount.Should().Be(1);
        stats.FirstCommunicationAt.Should().Be(new DateTime(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc));
        stats.LastCommunicationAt.Should().Be(new DateTime(2026, 2, 2, 10, 0, 0, DateTimeKind.Utc));

        stats.Timeline.Should().HaveCount(2);
        stats.Timeline[0].PeriodStart.Should().Be(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        stats.Timeline[0].TotalCount.Should().Be(2);
        stats.Timeline[1].PeriodStart.Should().Be(new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc));
        stats.Timeline[1].TotalCount.Should().Be(1);
    }

    private async Task<ContactDetailsDto> CreateContactAsync(string uid)
    {
        var contact = await PostTest<ContactDetailsDto>("/api/contacts", new TestContact(uid), HttpStatusCode.Created);
        contact.Should().NotBeNull();
        return contact!;
    }
}
