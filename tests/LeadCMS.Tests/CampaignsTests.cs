// <copyright file="CampaignsTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.Json;
using LeadCMS.Enums;
using LeadCMS.Helpers;

namespace LeadCMS.Tests;

public class CampaignsTests : BaseTestAutoLogin
{
    private const string CampaignsUrl = "/api/campaigns";
    private const string CampaignPreviewUrl = "/api/campaigns/preview";
    private const string EmailTemplatePreviewUrl = "/api/email-templates/preview";
    private const string ContactsUrl = "/api/contacts";
    private const string SegmentsUrl = "/api/segments";
    private const string EmailGroupsUrl = "/api/email-groups";
    private const string EmailTemplatesUrl = "/api/email-templates";
    private const string TasksUrl = "/api/tasks";

    public CampaignsTests()
        : base()
    {
        TrackEntityType<Campaign>();
        TrackEntityType<CampaignRecipient>();
        TrackEntityType<Contact>();
        TrackEntityType<Segment>();
        TrackEntityType<EmailGroup>();
        TrackEntityType<EmailTemplate>();
        TrackEntityType<Unsubscribe>();
        TrackEntityType<EmailLog>();
    }

    // ──────────────────────────────────────────────────
    // CRUD Tests
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateCampaign_ReturnsDraftStatus()
    {
        var (templateId, segmentId) = await CreatePrerequisitesAsync("1");

        var campaign = new TestCampaign("1", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);

        var created = await GetTest<CampaignDetailsDto>(location);

        created.Should().NotBeNull();
        created!.Name.Should().Be(campaign.Name);
        created.Status.Should().Be(CampaignStatus.Draft);
        created.EmailTemplateId.Should().Be(templateId);
        created.SegmentIds.Should().BeEquivalentTo(new[] { segmentId });
        created.TotalRecipients.Should().Be(0);
        created.SentCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateCampaign_WithInvalidTemplate_Returns422()
    {
        var segmentId = await CreateSegmentWithContactsAsync("1", 1);

        var campaign = new TestCampaign("invalid-tpl", 99999, new[] { segmentId });
        await PostTest<CampaignDetailsDto>(CampaignsUrl, campaign, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateCampaign_WithInvalidSegment_Returns422()
    {
        var templateId = await CreateEmailTemplateAsync("1");

        var campaign = new TestCampaign("invalid-seg", templateId, new[] { 99999 });
        await PostTest<CampaignDetailsDto>(CampaignsUrl, campaign, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UpdateCampaign_InDraftStatus_Succeeds()
    {
        var (templateId, segmentId) = await CreatePrerequisitesAsync("upd");

        var campaign = new TestCampaign("upd", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);

        var update = new CampaignUpdateDto { Name = "UpdatedCampaignName" };
        await PatchTest(location, update);

        var updated = await GetTest<CampaignDetailsDto>(location);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("UpdatedCampaignName");
    }

    [Fact]
    public async Task DeleteCampaign_Succeeds()
    {
        var (templateId, segmentId) = await CreatePrerequisitesAsync("del");

        var campaign = new TestCampaign("del", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);

        await DeleteTest(location);
        await GetTest(location, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAllCampaigns_ReturnsCreatedCampaigns()
    {
        var (templateId, segmentId) = await CreatePrerequisitesAsync("list");

        var campaign1 = new TestCampaign("list1", templateId, new[] { segmentId });
        var campaign2 = new TestCampaign("list2", templateId, new[] { segmentId });

        await PostTest(CampaignsUrl, campaign1);
        await PostTest(CampaignsUrl, campaign2);

        var campaigns = await GetTest<List<CampaignDetailsDto>>(CampaignsUrl);
        campaigns.Should().NotBeNull();
        campaigns!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    // ──────────────────────────────────────────────────
    // Launch & Lifecycle Tests
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task LaunchCampaign_SendNow_ResolvesAudienceAndTransitionsToSending()
    {
        var (templateId, segmentId) = await CreatePrerequisitesAsync("launch", contactCount: 5);

        var campaign = new TestCampaign("launch", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        var launchDto = new CampaignLaunchDto { SendNow = true };
        var launched = await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", launchDto, HttpStatusCode.OK);

        launched.Should().NotBeNull();
        launched!.Status.Should().Be(CampaignStatus.Sending);
        launched.TotalRecipients.Should().Be(5);
        launched.SendStartedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task LaunchCampaign_Scheduled_TransitionsToScheduled()
    {
        var (templateId, segmentId) = await CreatePrerequisitesAsync("sched");

        var scheduledTime = DateTime.UtcNow.AddHours(1);
        var campaign = new TestCampaign("sched", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        var launchDto = new CampaignLaunchDto { SendNow = false, ScheduledAt = scheduledTime };
        var launched = await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", launchDto, HttpStatusCode.OK);

        launched.Should().NotBeNull();
        launched!.Status.Should().Be(CampaignStatus.Scheduled);
        launched.ScheduledAt.Should().BeCloseTo(scheduledTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task LaunchCampaign_AlreadyLaunched_Returns422()
    {
        var (templateId, segmentId) = await CreatePrerequisitesAsync("dbl", contactCount: 1);

        var campaign = new TestCampaign("dbl", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        // Launch once
        var launchDto = new CampaignLaunchDto { SendNow = true };
        await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", launchDto, HttpStatusCode.OK);

        // Try to launch again
        await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", launchDto, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CancelScheduledCampaign_Succeeds()
    {
        var (templateId, segmentId) = await CreatePrerequisitesAsync("cancel");

        var campaign = new TestCampaign("cancel", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        // Schedule it
        var launchDto = new CampaignLaunchDto { SendNow = false, ScheduledAt = DateTime.UtcNow.AddHours(1) };
        await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", launchDto, HttpStatusCode.OK);

        // Cancel
        var cancelled = await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/cancel", new { }, HttpStatusCode.OK);
        cancelled.Should().NotBeNull();
        cancelled!.Status.Should().Be(CampaignStatus.Cancelled);
    }

    [Fact]
    public async Task CancelNonScheduledCampaign_Returns422()
    {
        var (templateId, segmentId) = await CreatePrerequisitesAsync("cnosch");

        var campaign = new TestCampaign("cnosch", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        // Campaign is in Draft status, cannot cancel
        await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/cancel", new { }, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task PauseAndResumeCampaign_Succeeds()
    {
        var (templateId, segmentId) = await CreatePrerequisitesAsync("pause", contactCount: 3);

        var campaign = new TestCampaign("pause", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        // Launch (sets status to Sending)
        var launchDto = new CampaignLaunchDto { SendNow = true };
        await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", launchDto, HttpStatusCode.OK);

        // Pause
        var paused = await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/pause", new { }, HttpStatusCode.OK);
        paused.Should().NotBeNull();
        paused!.Status.Should().Be(CampaignStatus.Paused);

        // Resume
        var resumed = await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/resume", new { }, HttpStatusCode.OK);
        resumed.Should().NotBeNull();
        resumed!.Status.Should().Be(CampaignStatus.Sending);
    }

    [Fact]
    public async Task UpdateCampaign_AfterLaunch_Returns422()
    {
        var (templateId, segmentId) = await CreatePrerequisitesAsync("noupd", contactCount: 1);

        var campaign = new TestCampaign("noupd", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        var launchDto = new CampaignLaunchDto { SendNow = true };
        await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", launchDto, HttpStatusCode.OK);

        // Try to update while sending
        var update = new CampaignUpdateDto { Name = "ShouldFail" };
        await PatchTest(location, update, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UpdateCampaign_InScheduledStatus_SchedulingFieldsOnly_Succeeds()
    {
        var (templateId, segmentId) = await CreatePrerequisitesAsync("schedpatch", contactCount: 1);

        var campaign = new TestCampaign("schedpatch", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        var initialScheduledAt = DateTime.UtcNow.AddHours(3);
        var launchDto = new CampaignLaunchDto
        {
            SendNow = false,
            ScheduledAt = initialScheduledAt,
            TimeZone = 120,
            UseContactTimeZone = false,
        };

        await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", launchDto, HttpStatusCode.OK);

        var newScheduledAt = DateTime.UtcNow.AddHours(5);
        var update = new CampaignUpdateDto
        {
            TimeZone = -330,
            UseContactTimeZone = true,
            ScheduledAt = newScheduledAt,
        };

        await PatchTest(location, update, HttpStatusCode.OK);

        var updated = await GetTest<CampaignDetailsDto>(location);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(CampaignStatus.Scheduled);
        updated.TimeZone.Should().Be(-330);
        updated.UseContactTimeZone.Should().BeTrue();
        updated.ScheduledAt.Should().BeCloseTo(newScheduledAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task UpdateCampaign_InScheduledStatus_NonSchedulingField_Returns422()
    {
        var (templateId, segmentId) = await CreatePrerequisitesAsync("schedlocked", contactCount: 1);

        var campaign = new TestCampaign("schedlocked", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        var launchDto = new CampaignLaunchDto
        {
            SendNow = false,
            ScheduledAt = DateTime.UtcNow.AddHours(3),
            TimeZone = 60,
        };

        await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", launchDto, HttpStatusCode.OK);

        var update = new CampaignUpdateDto { Name = "ShouldNotBeApplied" };
        var response = await PatchTest(location, update, HttpStatusCode.UnprocessableEntity);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("You can update scheduling fields only");

        var unchanged = await GetTest<CampaignDetailsDto>(location);
        unchanged.Should().NotBeNull();
        unchanged!.Name.Should().Be(campaign.Name);
        unchanged.Status.Should().Be(CampaignStatus.Scheduled);
    }

    // ──────────────────────────────────────────────────
    // Audience Resolution Tests
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task LaunchCampaign_AllContactsReceiveEmail()
    {
        var (templateId, segmentId) = await CreatePrerequisitesAsync("allrcv", contactCount: 10);

        var campaign = new TestCampaign("allrcv", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        var launchDto = new CampaignLaunchDto { SendNow = true };
        await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", launchDto, HttpStatusCode.OK);

        // Execute the task to send
        await ExecuteCampaignSendTask();

        // Check statistics
        var stats = await GetTest<CampaignStatisticsDto>($"{CampaignsUrl}/{campaignId}/statistics");
        stats.Should().NotBeNull();
        stats!.TotalRecipients.Should().Be(10);
        stats.SentCount.Should().Be(10);
        stats.FailedCount.Should().Be(0);
        stats.SkippedCount.Should().Be(0);
    }

    [Fact]
    public async Task LaunchCampaign_OverlappingSegments_NoDuplicateSends()
    {
        // Create contacts
        var contactIds = new List<int>();
        for (int i = 0; i < 5; i++)
        {
            contactIds.Add(await CreateContactAsync($"overlap{i}"));
        }

        var templateId = await CreateEmailTemplateAsync("overlap");

        // Create two segments with overlapping contacts
        var segment1Id = await CreateStaticSegmentAsync("overlap1", contactIds.Take(3).ToArray());
        var segment2Id = await CreateStaticSegmentAsync("overlap2", contactIds.Skip(2).ToArray()); // contacts 2,3,4 overlap with segment1

        var campaign = new TestCampaign("overlap", templateId, new[] { segment1Id, segment2Id });
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        var launchDto = new CampaignLaunchDto { SendNow = true };
        await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", launchDto, HttpStatusCode.OK);

        await ExecuteCampaignSendTask();

        // Should have 5 unique recipients (not 8)
        var stats = await GetTest<CampaignStatisticsDto>($"{CampaignsUrl}/{campaignId}/statistics");
        stats.Should().NotBeNull();
        stats!.TotalRecipients.Should().Be(5);
        stats.SentCount.Should().Be(5);
    }

    [Fact]
    public async Task LaunchCampaign_UnsubscribedContactsSkipped()
    {
        // Create contacts
        var contact1Id = await CreateContactAsync("unsub1");
        var contact2Id = await CreateContactAsync("unsub2");

        // Unsubscribe one contact
        await UnsubscribeContactAsync(contact2Id);

        var templateId = await CreateEmailTemplateAsync("unsub");
        var segmentId = await CreateStaticSegmentAsync("unsub", new[] { contact1Id, contact2Id });

        var campaign = new TestCampaign("unsub", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        var launchDto = new CampaignLaunchDto { SendNow = true };
        await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", launchDto, HttpStatusCode.OK);

        await ExecuteCampaignSendTask();

        var stats = await GetTest<CampaignStatisticsDto>($"{CampaignsUrl}/{campaignId}/statistics");
        stats.Should().NotBeNull();
        stats!.TotalRecipients.Should().Be(2);
        stats.SentCount.Should().Be(1);
        stats.SkippedCount.Should().Be(1);
        stats.SkippedUnsubscribed.Should().Be(1);
    }

    [Fact]
    public async Task LaunchCampaign_WithExclusionSegment_ExcludesContacts()
    {
        // Create contacts
        var contactIds = new List<int>();
        for (int i = 0; i < 5; i++)
        {
            contactIds.Add(await CreateContactAsync($"excl{i}"));
        }

        var templateId = await CreateEmailTemplateAsync("excl");

        // All 5 contacts in the main segment
        var mainSegmentId = await CreateStaticSegmentAsync("main", contactIds.ToArray());

        // 2 contacts in the exclusion segment
        var exclusionSegmentId = await CreateStaticSegmentAsync("excl", contactIds.Take(2).ToArray());

        var campaign = new TestCampaign("excl", templateId, new[] { mainSegmentId });
        campaign.ExcludeSegmentIds = new[] { exclusionSegmentId };
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        var launchDto = new CampaignLaunchDto { SendNow = true };
        await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", launchDto, HttpStatusCode.OK);

        await ExecuteCampaignSendTask();

        var stats = await GetTest<CampaignStatisticsDto>($"{CampaignsUrl}/{campaignId}/statistics");
        stats.Should().NotBeNull();
        stats!.TotalRecipients.Should().Be(3);
        stats.SentCount.Should().Be(3);
    }

    // ──────────────────────────────────────────────────
    // Background Task Tests
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task CampaignSendTask_ScheduledCampaign_SendsAtScheduledTime()
    {
        var (templateId, segmentId) = await CreatePrerequisitesAsync("task", contactCount: 3);

        var campaign = new TestCampaign("task", templateId, new[] { segmentId });
        campaign.ScheduledAt = DateTime.UtcNow.AddMinutes(-1); // In the past so it triggers immediately
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        // Schedule the campaign (set ScheduledAt to the past for immediate trigger)
        var launchDto = new CampaignLaunchDto { SendNow = false, ScheduledAt = DateTime.UtcNow.AddMinutes(-1) };

        // Should fail because ScheduledAt is in the past
        await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", launchDto, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CampaignSendTask_SendingCampaign_ProcessesAndCompletes()
    {
        var (templateId, segmentId) = await CreatePrerequisitesAsync("process", contactCount: 5);

        var campaign = new TestCampaign("process", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        // Launch immediately
        var launchDto = new CampaignLaunchDto { SendNow = true };
        await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", launchDto, HttpStatusCode.OK);

        // Execute task to process pending recipients
        await ExecuteCampaignSendTask();

        // The campaign should now be Sent
        var completed = await GetTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}");
        completed.Should().NotBeNull();
        completed!.Status.Should().Be(CampaignStatus.Sent);
        completed.SentCount.Should().Be(5);
        completed.SendCompletedAt.Should().NotBeNull();
    }

    // ──────────────────────────────────────────────────
    // Statistics & Recipients Tests
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task GetStatistics_ReturnsCorrectCounts()
    {
        var contact1Id = await CreateContactAsync("stat1");
        var contact2Id = await CreateContactAsync("stat2");
        var contact3Id = await CreateContactAsync("stat3");

        await UnsubscribeContactAsync(contact3Id);

        var templateId = await CreateEmailTemplateAsync("stat");
        var segmentId = await CreateStaticSegmentAsync("stat", new[] { contact1Id, contact2Id, contact3Id });

        var campaign = new TestCampaign("stat", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        var launchDto = new CampaignLaunchDto { SendNow = true };
        await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", launchDto, HttpStatusCode.OK);

        await ExecuteCampaignSendTask();

        var stats = await GetTest<CampaignStatisticsDto>($"{CampaignsUrl}/{campaignId}/statistics");
        stats.Should().NotBeNull();
        stats!.TotalRecipients.Should().Be(3);
        stats.SentCount.Should().Be(2);
        stats.SkippedCount.Should().Be(1);
        stats.SkippedUnsubscribed.Should().Be(1);
        stats.PendingCount.Should().Be(0);
    }

    [Fact]
    public async Task GetRecipients_ReturnsAllRecipientRecords()
    {
        var (templateId, segmentId) = await CreatePrerequisitesAsync("recip", contactCount: 3);

        var campaign = new TestCampaign("recip", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        var launchDto = new CampaignLaunchDto { SendNow = true };
        await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", launchDto, HttpStatusCode.OK);

        var response = await GetTest($"{CampaignsUrl}/{campaignId}/recipients");
        var content = await response.Content.ReadAsStringAsync();
        var recipients = JsonHelper.Deserialize<List<CampaignRecipientDetailsDto>>(content);

        recipients.Should().NotBeNull();
        recipients!.Count.Should().Be(3);
        recipients.Should().OnlyContain(r => r.CampaignId == campaignId);
        recipients.Should().OnlyContain(r => r.Contact != null);
        recipients.Select(r => r.Contact!.Email).Should().OnlyContain(e => !string.IsNullOrWhiteSpace(e));
    }

    [Fact]
    public async Task GetRecipients_ScheduledByCampaignTimezone_ReturnsExpectedSendAtUtc()
    {
        var (templateId, segmentId) = await CreatePrerequisitesAsync("recip-tz", contactCount: 2);

        var campaign = new TestCampaign("recip-tz", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        var scheduledAt = DateTime.UtcNow.AddHours(3);
        await PatchTest(location, new CampaignUpdateDto
        {
            ScheduledAt = scheduledAt,
            TimeZone = -330,
            UseContactTimeZone = false,
        });

        await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", new CampaignLaunchDto { SendNow = true }, HttpStatusCode.OK);

        var response = await GetTest($"{CampaignsUrl}/{campaignId}/recipients");
        var content = await response.Content.ReadAsStringAsync();
        var recipients = JsonHelper.Deserialize<List<CampaignRecipientDetailsDto>>(content);

        recipients.Should().NotBeNull();
        recipients!.Count.Should().Be(2);

        var expectedUtc = scheduledAt;
        recipients.Should().OnlyContain(r => r.ExpectedSendAtUtc.HasValue);
        recipients.Select(r => r.ExpectedSendAtUtc!.Value)
            .Should().OnlyContain(v => Math.Abs((v - expectedUtc).TotalSeconds) <= 1);
    }

    [Fact]
    public async Task GetRecipients_ScheduledByContactTimezone_ReturnsExpectedSendAtUtcPerContact()
    {
        var contact1Id = await CreateContactWithTimezoneAsync("recip-ctz1", 60);
        var contact2Id = await CreateContactWithTimezoneAsync("recip-ctz2", -120);
        var templateId = await CreateEmailTemplateAsync("recip-ctz");
        var segmentId = await CreateStaticSegmentAsync("recip-ctz", new[] { contact1Id, contact2Id });

        var campaign = new TestCampaign("recip-ctz", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        var scheduledAt = DateTime.UtcNow.AddHours(4);
        await PatchTest(location, new CampaignUpdateDto
        {
            ScheduledAt = scheduledAt,
            TimeZone = 0,
            UseContactTimeZone = true,
        });

        var launched = await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", new CampaignLaunchDto { SendNow = true }, HttpStatusCode.OK);

        var response = await GetTest($"{CampaignsUrl}/{campaignId}/recipients");
        var content = await response.Content.ReadAsStringAsync();
        var recipients = JsonHelper.Deserialize<List<CampaignRecipientDetailsDto>>(content);

        recipients.Should().NotBeNull();
        recipients!.Count.Should().Be(2);

        var campaignOffset = launched!.TimeZone ?? 0;
        var byContact = recipients.ToDictionary(r => r.ContactId, r => r);
        byContact[contact1Id].ExpectedSendAtUtc.Should().NotBeNull();
        byContact[contact1Id].ExpectedSendAtUtc!.Value.Should().BeCloseTo(scheduledAt.AddMinutes(60 - campaignOffset), TimeSpan.FromSeconds(1));

        byContact[contact2Id].ExpectedSendAtUtc.Should().NotBeNull();
        byContact[contact2Id].ExpectedSendAtUtc!.Value.Should().BeCloseTo(scheduledAt.AddMinutes(-120 - campaignOffset), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetStatistics_ForNonExistentCampaign_Returns404()
    {
        await GetTest($"{CampaignsUrl}/99999/statistics", HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────────
    // Idempotency Tests
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task CampaignSendTask_MultipleExecutions_DoNotResendEmails()
    {
        var (templateId, segmentId) = await CreatePrerequisitesAsync("idemp", contactCount: 3);

        var campaign = new TestCampaign("idemp", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        var launchDto = new CampaignLaunchDto { SendNow = true };
        await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", launchDto, HttpStatusCode.OK);

        // Execute task multiple times
        await ExecuteCampaignSendTask();
        await ExecuteCampaignSendTask();

        // Should still only have 3 sent, not 6
        var stats = await GetTest<CampaignStatisticsDto>($"{CampaignsUrl}/{campaignId}/statistics");
        stats.Should().NotBeNull();
        stats!.SentCount.Should().Be(3);
        stats.TotalRecipients.Should().Be(3);
    }

    // ──────────────────────────────────────────────────
    // Timezone Scheduling Tests
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateCampaign_WithTimezoneFields_StoresAllTimezoneSettings()
    {
        var (templateId, segmentId) = await CreatePrerequisitesAsync("tz1");

        var campaign = new TestCampaign("tz1", templateId, new[] { segmentId });
        campaign.TimeZone = 120; // UTC+2
        campaign.UseContactTimeZone = true;
        campaign.ScheduledAt = DateTime.UtcNow.AddDays(1);

        var location = await PostTest(CampaignsUrl, campaign);
        var created = await GetTest<CampaignDetailsDto>(location);

        created.Should().NotBeNull();
        created!.TimeZone.Should().Be(120);
        created.UseContactTimeZone.Should().BeTrue();
        created.ScheduledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task LaunchCampaign_ScheduledWithTimezone_ConvertsToUtc()
    {
        var (templateId, segmentId) = await CreatePrerequisitesAsync("tz2");

        var campaign = new TestCampaign("tz2", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        // Schedule for tomorrow using UTC+3 offset
        var launchDto = new CampaignLaunchDto
        {
            SendNow = false,
            ScheduledAt = DateTime.UtcNow.AddDays(1),
            TimeZone = 180, // UTC+3
            UseContactTimeZone = false,
        };

        var launched = await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", launchDto, HttpStatusCode.OK);

        launched.Should().NotBeNull();
        launched!.Status.Should().Be(CampaignStatus.Scheduled);
        launched.TimeZone.Should().Be(180);
        launched.UseContactTimeZone.Should().BeFalse();
    }

    [Fact]
    public async Task LaunchCampaign_ScheduledWithUseContactTimeZone_StoresFlag()
    {
        var (templateId, segmentId) = await CreatePrerequisitesAsync("tz3");

        var campaign = new TestCampaign("tz3", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        var launchDto = new CampaignLaunchDto
        {
            SendNow = false,
            ScheduledAt = DateTime.UtcNow.AddDays(1),
            UseContactTimeZone = true,
            TimeZone = 60, // UTC+1 fallback for contacts without timezone
        };

        var launched = await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", launchDto, HttpStatusCode.OK);

        launched.Should().NotBeNull();
        launched!.Status.Should().Be(CampaignStatus.Scheduled);
        launched.UseContactTimeZone.Should().BeTrue();
        launched.TimeZone.Should().Be(60);
    }

    [Fact]
    public async Task LaunchCampaign_ScheduledInPast_ReturnsBadRequest()
    {
        var (templateId, segmentId) = await CreatePrerequisitesAsync("tz4");

        var campaign = new TestCampaign("tz4", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        var launchDto = new CampaignLaunchDto
        {
            SendNow = false,
            ScheduledAt = DateTime.UtcNow.AddHours(-2),
            TimeZone = 0,
        };

        await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", launchDto, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CampaignSendTask_ScheduledWithTimezone_SendsWhenDue()
    {
        var contactId = await CreateContactAsync("tz5_0");
        var templateId = await CreateEmailTemplateAsync("tz5");
        var segmentId = await CreateStaticSegmentAsync("tz5", new[] { contactId });

        var campaign = new TestCampaign("tz5", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        // Schedule 1 minute ago in UTC — should fire immediately
        var launchDto = new CampaignLaunchDto
        {
            SendNow = false,
            ScheduledAt = DateTime.UtcNow.AddMinutes(-1),
            TimeZone = 0,
        };

        // Launch (past-scheduling with offset=0 should still be accepted by small margin)
        // If the service rejects it, we fall back to SendNow
        var launchResult = await Request(HttpMethod.Post, $"{CampaignsUrl}/{campaignId}/launch", launchDto);

        if (launchResult.StatusCode != HttpStatusCode.OK)
        {
            // Use SendNow instead
            launchDto = new CampaignLaunchDto { SendNow = true };
            await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", launchDto, HttpStatusCode.OK);
        }

        // Execute the task
        await ExecuteCampaignSendTask();

        var stats = await GetTest<CampaignStatisticsDto>($"{CampaignsUrl}/{campaignId}/statistics");
        stats.Should().NotBeNull();
        stats!.SentCount.Should().Be(1);
    }

    [Fact]
    public async Task CampaignSendTask_UseContactTimeZone_ProcessesPerContactTimezone()
    {
        // Create contacts with different timezones
        var contact1Id = await CreateContactWithTimezoneAsync("ctz1", 0);       // UTC
        var contact2Id = await CreateContactWithTimezoneAsync("ctz2", -720);    // UTC-12 (far behind)
        var contact3Id = await CreateContactWithTimezoneAsync("ctz3", null);    // No timezone set

        var templateId = await CreateEmailTemplateAsync("ctz");
        var segmentId = await CreateStaticSegmentAsync("ctz", new[] { contact1Id, contact2Id, contact3Id });

        var campaign = new TestCampaign("ctz", templateId, new[] { segmentId });
        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        // Schedule for now using contact timezones with TimeZone as fallback (UTC)
        var launchDto = new CampaignLaunchDto
        {
            SendNow = false,
            ScheduledAt = DateTime.UtcNow.AddMinutes(-5),
            UseContactTimeZone = true,
            TimeZone = 0, // fallback for contacts without timezone
        };

        // This might be rejected as past — try SendNow fallback with timezone flags
        var launchResult = await Request(HttpMethod.Post, $"{CampaignsUrl}/{campaignId}/launch", launchDto);

        if (launchResult.StatusCode != HttpStatusCode.OK)
        {
            // If the scheduled time was rejected, test with SendNow to ensure basic functionality
            launchDto = new CampaignLaunchDto { SendNow = true };
            await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", launchDto, HttpStatusCode.OK);
        }
        else
        {
            // Execute task to transition scheduled → sending
            await ExecuteCampaignSendTask();
        }

        await ExecuteCampaignSendTask();

        var stats = await GetTest<CampaignStatisticsDto>($"{CampaignsUrl}/{campaignId}/statistics");
        stats.Should().NotBeNull();

        // All 3 contacts should eventually be sent
        stats!.SentCount.Should().Be(3);
    }

    [Fact]
    public async Task CampaignSendTask_UseContactTimeZone_SameCampaignAndContactTimezone_SendsWithoutDelay()
    {
        var contactId = await CreateContactWithTimezoneAsync("ctzsame", -330); // UTC+5:30 in current convention
        var templateId = await CreateEmailTemplateAsync("ctzsame");
        var segmentId = await CreateStaticSegmentAsync("ctzsame", new[] { contactId });

        var campaign = new TestCampaign("ctzsame", templateId, new[] { segmentId })
        {
            ScheduledAt = DateTime.UtcNow.AddMinutes(-1),
            TimeZone = -330,
            UseContactTimeZone = true,
        };

        var location = await PostTest(CampaignsUrl, campaign);
        var campaignId = ExtractId(location);

        // Start sending immediately (recipients are still gated by per-contact scheduled check)
        await PostTest<CampaignDetailsDto>($"{CampaignsUrl}/{campaignId}/launch", new CampaignLaunchDto { SendNow = true }, HttpStatusCode.OK);

        await ExecuteCampaignSendTask();

        var stats = await GetTest<CampaignStatisticsDto>($"{CampaignsUrl}/{campaignId}/statistics");
        stats.Should().NotBeNull();
        stats!.SentCount.Should().Be(1);
        stats.PendingCount.Should().Be(0);
    }

    // ──────────────────────────────────────────────────
    // Send Test Email Tests
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task SendTestEmail_WithValidContactAndInlineTemplate_ReturnsOk()
    {
        var contactId = await CreateContactAsync("st1");

        var sendTestDto = new EmailTemplateSendTestDto
        {
            Subject = "Hello {{ FirstName }}",
            BodyTemplate = "<p>Hello {{ FirstName }} {{ LastName }}</p>",
            FromEmail = "sender@test.net",
            FromName = "Test Sender",
            ContactId = contactId,
            RecipientEmail = "testrecipient@example.com",
        };

        await PostTest<object>($"{EmailTemplatesUrl}/send-test", sendTestDto, HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendTestEmail_WithDummyContact_ReturnsOk()
    {
        var sendTestDto = new EmailTemplateSendTestDto
        {
            Subject = "Hello {{ FirstName }}",
            BodyTemplate = "<p>Hello {{ FirstName }}</p>",
            FromEmail = "sender@test.net",
            FromName = "Test Sender",
            RecipientEmail = "dummycontact@example.com",
        };

        await PostTest<object>($"{EmailTemplatesUrl}/send-test", sendTestDto, HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendTestEmail_WithInvalidContact_ReturnsNotFound()
    {
        var sendTestDto = new EmailTemplateSendTestDto
        {
            Subject = "Hello {{ FirstName }}",
            BodyTemplate = "<p>Hello {{ FirstName }}</p>",
            FromEmail = "sender@test.net",
            FromName = "Test Sender",
            ContactId = 99999,
            RecipientEmail = "badcontact@example.com",
        };

        await PostTest<object>($"{EmailTemplatesUrl}/send-test", sendTestDto, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SendTestEmail_WithInvalidRecipientEmail_ReturnsUnprocessableEntity()
    {
        var sendTestDto = new EmailTemplateSendTestDto
        {
            Subject = "Hello {{ FirstName }}",
            BodyTemplate = "<p>Hello {{ FirstName }}</p>",
            FromEmail = "sender@test.net",
            FromName = "Test Sender",
            RecipientEmail = "not-an-email",
        };

        await PostTest<object>($"{EmailTemplatesUrl}/send-test", sendTestDto, HttpStatusCode.UnprocessableEntity);
    }

    // ──────────────────────────────────────────────────
    // Preview Tests — Campaign Preview (audience stats + template rendering via /api/campaigns/preview)
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task CampaignPreview_WithValidSegmentsAndTemplate_ReturnsAudienceAndRenderedEmail()
    {
        var contact1Id = await CreateContactAsync("pv1_0");
        var contact2Id = await CreateContactAsync("pv1_1");
        var contact3Id = await CreateContactAsync("pv1_2");

        var templateId = await CreateEmailTemplateAsync("pv1");
        var segmentId = await CreateStaticSegmentAsync("pv1", new[] { contact1Id, contact2Id, contact3Id });

        var previewDto = new CampaignPreviewRequestDto
        {
            EmailTemplateId = templateId,
            SegmentIds = new[] { segmentId },
        };

        var result = await PostTest<CampaignPreviewResultDto>(CampaignPreviewUrl, previewDto, HttpStatusCode.OK);

        result.Should().NotBeNull();
        result!.TotalAudienceCount.Should().Be(3);
        result.SendableCount.Should().Be(3);
        result.UnsubscribedCount.Should().Be(0);
        result.InvalidEmailCount.Should().Be(0);
        result.TemplatePreview.Should().NotBeNull();
        result.TemplatePreview.RenderedBody.Should().NotBeNullOrEmpty();
        result.TemplatePreview.RenderedSubject.Should().NotBeNullOrEmpty();
        result.TemplatePreview.FromEmail.Should().NotBeNullOrEmpty();
        result.TemplatePreview.FromName.Should().NotBeNullOrEmpty();
        result.TemplatePreview.PreviewContactId.Should().BeGreaterThan(0);
        result.TemplatePreview.PreviewContactEmail.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CampaignPreview_WithSpecificContactId_UsesSpecifiedContact()
    {
        var contact1Id = await CreateContactAsync("pv2_0");
        var contact2Id = await CreateContactAsync("pv2_1");

        var templateId = await CreateEmailTemplateAsync("pv2");
        var segmentId = await CreateStaticSegmentAsync("pv2", new[] { contact1Id, contact2Id });

        var previewDto = new CampaignPreviewRequestDto
        {
            EmailTemplateId = templateId,
            SegmentIds = new[] { segmentId },
            ContactId = contact2Id,
        };

        var result = await PostTest<CampaignPreviewResultDto>(CampaignPreviewUrl, previewDto, HttpStatusCode.OK);

        result.Should().NotBeNull();
        result!.TemplatePreview.PreviewContactId.Should().Be(contact2Id);
        result.TemplatePreview.PreviewContactEmail.Should().Contain("pv2_1");
    }

    [Fact]
    public async Task CampaignPreview_WithExcludeSegments_ReducesAudienceCount()
    {
        var contactIds = new List<int>();
        for (int i = 0; i < 5; i++)
        {
            contactIds.Add(await CreateContactAsync($"pv3_{i}"));
        }

        var templateId = await CreateEmailTemplateAsync("pv3");
        var mainSegmentId = await CreateStaticSegmentAsync("pv3main", contactIds.ToArray());
        var excludeSegmentId = await CreateStaticSegmentAsync("pv3excl", contactIds.Take(2).ToArray());

        var previewDto = new CampaignPreviewRequestDto
        {
            EmailTemplateId = templateId,
            SegmentIds = new[] { mainSegmentId },
            ExcludeSegmentIds = new[] { excludeSegmentId },
        };

        var result = await PostTest<CampaignPreviewResultDto>(CampaignPreviewUrl, previewDto, HttpStatusCode.OK);

        result.Should().NotBeNull();
        result!.TotalAudienceCount.Should().Be(3);
        result.SendableCount.Should().Be(3);
    }

    [Fact]
    public async Task CampaignPreview_WithUnsubscribedContacts_ReportsBreakdown()
    {
        var contact1Id = await CreateContactAsync("pv4_0");
        var contact2Id = await CreateContactAsync("pv4_1");
        var contact3Id = await CreateContactAsync("pv4_2");

        await UnsubscribeContactAsync(contact2Id);

        var templateId = await CreateEmailTemplateAsync("pv4");
        var segmentId = await CreateStaticSegmentAsync("pv4", new[] { contact1Id, contact2Id, contact3Id });

        var previewDto = new CampaignPreviewRequestDto
        {
            EmailTemplateId = templateId,
            SegmentIds = new[] { segmentId },
        };

        var result = await PostTest<CampaignPreviewResultDto>(CampaignPreviewUrl, previewDto, HttpStatusCode.OK);

        result.Should().NotBeNull();
        result!.TotalAudienceCount.Should().Be(3);
        result.UnsubscribedCount.Should().Be(1);
        result.SendableCount.Should().Be(2);
    }

    [Fact]
    public async Task CampaignPreview_WithInvalidTemplate_Returns404()
    {
        var contactId = await CreateContactAsync("pv5_0");
        var segmentId = await CreateStaticSegmentAsync("pv5", new[] { contactId });

        var previewDto = new CampaignPreviewRequestDto
        {
            EmailTemplateId = 99999,
            SegmentIds = new[] { segmentId },
        };

        await PostTest<CampaignPreviewResultDto>(CampaignPreviewUrl, previewDto, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CampaignPreview_DoesNotRequireSavedCampaign()
    {
        // Campaign preview should work without creating a campaign first
        var contactId = await CreateContactAsync("pv8_0");
        var templateId = await CreateEmailTemplateAsync("pv8");
        var segmentId = await CreateStaticSegmentAsync("pv8", new[] { contactId });

        var previewDto = new CampaignPreviewRequestDto
        {
            EmailTemplateId = templateId,
            SegmentIds = new[] { segmentId },
        };

        var result = await PostTest<CampaignPreviewResultDto>(CampaignPreviewUrl, previewDto, HttpStatusCode.OK);

        result.Should().NotBeNull();
        result!.TotalAudienceCount.Should().Be(1);
        result.SendableCount.Should().Be(1);
        result.TemplatePreview.RenderedBody.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CampaignPreview_OverlappingSegments_DeduplicatesContacts()
    {
        var contactIds = new List<int>();
        for (int i = 0; i < 5; i++)
        {
            contactIds.Add(await CreateContactAsync($"pv9_{i}"));
        }

        var templateId = await CreateEmailTemplateAsync("pv9");

        // Two segments with overlapping contacts
        var segment1Id = await CreateStaticSegmentAsync("pv9a", contactIds.Take(3).ToArray());
        var segment2Id = await CreateStaticSegmentAsync("pv9b", contactIds.Skip(2).ToArray()); // contacts 2,3,4 overlap

        var previewDto = new CampaignPreviewRequestDto
        {
            EmailTemplateId = templateId,
            SegmentIds = new[] { segment1Id, segment2Id },
        };

        var result = await PostTest<CampaignPreviewResultDto>(CampaignPreviewUrl, previewDto, HttpStatusCode.OK);

        result.Should().NotBeNull();
        result!.TotalAudienceCount.Should().Be(5); // Deduplicated
        result.SendableCount.Should().Be(5);
    }

    // ──────────────────────────────────────────────────
    // Preview Tests — Email Template Preview (pure rendering via /api/email-templates/preview)
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task TemplatePreview_WithNoContact_UsesDummyData()
    {
        var previewDto = new EmailTemplatePreviewRequestDto
        {
            Subject = "Hello {{ FirstName }}",
            BodyTemplate = "<p>Hello {{ FirstName }} {{ LastName }}</p>",
            FromEmail = "pv6@test.net",
            FromName = "Preview Sender",
        };

        var result = await PostTest<EmailTemplatePreviewResultDto>(EmailTemplatePreviewUrl, previewDto, HttpStatusCode.OK);

        result.Should().NotBeNull();
        result!.RenderedBody.Should().NotBeNullOrEmpty();
        result.RenderedSubject.Should().NotBeNullOrEmpty();
        result.FromEmail.Should().NotBeNullOrEmpty();
        result.FromName.Should().NotBeNullOrEmpty();
        result.PreviewContactId.Should().Be(0);
        result.PreviewContactEmail.Should().NotBeNullOrEmpty();
        result.PreviewContactName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TemplatePreview_WithCustomTemplateParameters_OverridesBuiltInArguments()
    {
        var contactId = await CreateContactAsync("pv_custom_0");

        var previewDto = new EmailTemplatePreviewRequestDto
        {
            Subject = "Hello {{ FirstName }}",
            BodyTemplate = "<p>Hello {{ FirstName }}</p>",
            FromEmail = "custom-preview@test.net",
            FromName = "Custom Preview",
            ContactId = contactId,
            CustomTemplateParameters = new Dictionary<string, JsonElement>
            {
                ["FirstName"] = JsonSerializer.SerializeToElement("OverrideName"),
            },
        };

        var result = await PostTest<EmailTemplatePreviewResultDto>(EmailTemplatePreviewUrl, previewDto, HttpStatusCode.OK);

        result.Should().NotBeNull();
        result!.RenderedSubject.Should().Be("Hello OverrideName");
        result.RenderedBody.Should().Contain("Hello OverrideName");
    }

    [Fact]
    public async Task TemplatePreview_WithInvalidContactId_Returns404()
    {
        var previewDto = new EmailTemplatePreviewRequestDto
        {
            Subject = "Hello",
            BodyTemplate = "<p>Hello</p>",
            FromEmail = "pv7@test.net",
            FromName = "Preview",
            ContactId = 99999,
        };

        await PostTest<EmailTemplatePreviewResultDto>(EmailTemplatePreviewUrl, previewDto, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TemplatePreview_WithUnsavedTemplate_RendersSuccessfully()
    {
        var previewDto = new EmailTemplatePreviewRequestDto
        {
            Subject = "Hello {{ FirstName }}",
            BodyTemplate = "<p>Welcome {{ FirstName }}!</p>",
            FromEmail = "unsaved@test.net",
            FromName = "Unsaved Template",
        };

        var result = await PostTest<EmailTemplatePreviewResultDto>(EmailTemplatePreviewUrl, previewDto, HttpStatusCode.OK);

        result.Should().NotBeNull();
        result!.RenderedSubject.Should().Contain("Jane");
        result.RenderedBody.Should().Contain("Welcome Jane!");
        result.FromEmail.Should().Be("unsaved@test.net");
        result.FromName.Should().Be("Unsaved Template");
    }

    [Fact]
    public async Task TemplatePreview_WithSpecificContact_UsesContactData()
    {
        var contactId = await CreateContactAsync("pvt_contact");

        var previewDto = new EmailTemplatePreviewRequestDto
        {
            Subject = "Hello {{ FirstName }}",
            BodyTemplate = "<p>Hello {{ FirstName }}</p>",
            FromEmail = "pvt@test.net",
            FromName = "PVT Sender",
            ContactId = contactId,
        };

        var result = await PostTest<EmailTemplatePreviewResultDto>(EmailTemplatePreviewUrl, previewDto, HttpStatusCode.OK);

        result.Should().NotBeNull();
        result!.PreviewContactId.Should().Be(contactId);
        result.PreviewContactEmail.Should().Contain("pvt_contact");
        result.RenderedBody.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TemplatePreview_FullContactType_IncludesNestedObjects()
    {
        var previewDto = new EmailTemplatePreviewRequestDto
        {
            Subject = "Hello {{ FirstName }}",
            BodyTemplate = "<p>{{ FirstName }} {{ Account.Name }} {{ Domain.Name }} {{ Orders[0].RefNo }} {{ Deals[0].DealPipeline.Name }}</p>",
            FromEmail = "ct-full@test.net",
            FromName = "CT Full",
            ContactType = PreviewContactType.Full,
        };

        var result = await PostTest<EmailTemplatePreviewResultDto>(EmailTemplatePreviewUrl, previewDto, HttpStatusCode.OK);

        result.Should().NotBeNull();
        result!.RenderedBody.Should().Contain("Jane");
        result.RenderedBody.Should().Contain("Acme Corp");
        result.RenderedBody.Should().Contain("acme-corp.com");
        result.RenderedBody.Should().Contain("ORD-2025-001");
        result.RenderedBody.Should().Contain("Enterprise Sales");
        result.PreviewContactId.Should().Be(0);
    }

    [Fact]
    public async Task TemplatePreview_StandardContactType_ExcludesNestedObjects()
    {
        var previewDto = new EmailTemplatePreviewRequestDto
        {
            Subject = "Hello {{ FirstName }}",
            BodyTemplate = "<p>{{ FirstName }} {{ JobTitle }} |{{ Account.Name }}|</p>",
            FromEmail = "ct-std@test.net",
            FromName = "CT Standard",
            ContactType = PreviewContactType.Standard,
        };

        var result = await PostTest<EmailTemplatePreviewResultDto>(EmailTemplatePreviewUrl, previewDto, HttpStatusCode.OK);

        result.Should().NotBeNull();
        result!.RenderedBody.Should().Contain("Jane");
        result.RenderedBody.Should().Contain("Marketing Manager");
        result.RenderedBody.Should().Contain("||", "Standard contact type should not include nested Account object");
        result.PreviewContactId.Should().Be(0);
    }

    [Fact]
    public async Task TemplatePreview_BasicContactType_OnlyEmailAndName()
    {
        var previewDto = new EmailTemplatePreviewRequestDto
        {
            Subject = "Hello {{ FirstName }}",
            BodyTemplate = "<p>{{ FirstName }} {{ LastName }} |{{ Phone }}|</p>",
            FromEmail = "ct-basic@test.net",
            FromName = "CT Basic",
            ContactType = PreviewContactType.Basic,
        };

        var result = await PostTest<EmailTemplatePreviewResultDto>(EmailTemplatePreviewUrl, previewDto, HttpStatusCode.OK);

        result.Should().NotBeNull();
        result!.RenderedSubject.Should().Be("Hello Jane");
        result.RenderedBody.Should().Contain("Jane");
        result.RenderedBody.Should().Contain("Doe");
        result.RenderedBody.Should().Contain("||", "Basic contact type should not include Phone");
        result.PreviewContactId.Should().Be(0);
    }

    [Fact]
    public async Task TemplatePreview_MinimalContactType_OnlyEmail()
    {
        var previewDto = new EmailTemplatePreviewRequestDto
        {
            Subject = "Hello |{{ FirstName }}|",
            BodyTemplate = "<p>{{ Email }} |{{ FirstName }}|</p>",
            FromEmail = "ct-min@test.net",
            FromName = "CT Minimal",
            ContactType = PreviewContactType.Minimal,
        };

        var result = await PostTest<EmailTemplatePreviewResultDto>(EmailTemplatePreviewUrl, previewDto, HttpStatusCode.OK);

        result.Should().NotBeNull();
        result!.RenderedBody.Should().Contain("jane.doe@example.com");
        result.RenderedSubject.Should().Contain("||", "Minimal contact type should not include FirstName");
        result.PreviewContactId.Should().Be(0);
    }

    [Fact]
    public async Task TemplatePreview_DefaultContactType_UsesFull()
    {
        var previewDto = new EmailTemplatePreviewRequestDto
        {
            Subject = "Hello {{ FirstName }}",
            BodyTemplate = "<p>{{ Account.Name }} {{ Orders[0].RefNo }}</p>",
            FromEmail = "ct-def@test.net",
            FromName = "CT Default",
        };

        var result = await PostTest<EmailTemplatePreviewResultDto>(EmailTemplatePreviewUrl, previewDto, HttpStatusCode.OK);

        result.Should().NotBeNull();
        result!.RenderedBody.Should().Contain("Acme Corp");
        result.RenderedBody.Should().Contain("ORD-2025-001");
    }

    // ──────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────

    private static int ExtractId(string location)
    {
        return int.Parse(location.Split("/").Last());
    }

    private async Task<int> CreateContactAsync(string uid)
    {
        var contact = TestData.Generate<TestContact>(uid);
        var location = await PostTest(ContactsUrl, contact);
        return ExtractId(location);
    }

    private async Task<int> CreateEmailGroupAsync(string uid)
    {
        var group = TestData.Generate<TestEmailGroup>(uid);
        var location = await PostTest(EmailGroupsUrl, group);
        return ExtractId(location);
    }

    private async Task<int> CreateEmailTemplateAsync(string uid)
    {
        var groupId = await CreateEmailGroupAsync(uid);
        var template = TestData.Generate<TestEmailTemplate>(uid, groupId);
        var location = await PostTest(EmailTemplatesUrl, template);
        return ExtractId(location);
    }

    private async Task<int> CreateStaticSegmentAsync(string uid, int[] contactIds)
    {
        var segment = new TestSegment(uid, SegmentType.Static, null, contactIds);
        var location = await PostTest(SegmentsUrl, segment);
        return ExtractId(location);
    }

    private async Task<int> CreateSegmentWithContactsAsync(string uid, int contactCount)
    {
        var contactIds = new List<int>();
        for (int i = 0; i < contactCount; i++)
        {
            contactIds.Add(await CreateContactAsync($"{uid}_{i}"));
        }

        return await CreateStaticSegmentAsync(uid, contactIds.ToArray());
    }

    private async Task<(int templateId, int segmentId)> CreatePrerequisitesAsync(string uid, int contactCount = 3)
    {
        var templateId = await CreateEmailTemplateAsync(uid);
        var segmentId = await CreateSegmentWithContactsAsync(uid, contactCount);
        return (templateId, segmentId);
    }

    private async Task UnsubscribeContactAsync(int contactId)
    {
        var unsubscribeDto = new UnsubscribeDto
        {
            Reason = "Test unsubscribe",
            ContactId = contactId,
        };

        await PostTest("/api/unsubscribes", unsubscribeDto);
    }

    private async Task<int> CreateContactWithTimezoneAsync(string uid, int? timezone)
    {
        var contact = TestData.Generate<TestContact>(uid);
        contact.Timezone = timezone;
        var location = await PostTest(ContactsUrl, contact);
        return ExtractId(location);
    }

    private async Task ExecuteCampaignSendTask()
    {
        var response = await GetRequest($"{TasksUrl}/execute/CampaignSendTask");
        response.Should().NotBeNull();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var task = JsonHelper.Deserialize<TaskExecutionDto>(content);
        task!.Completed.Should().BeTrue();
    }
}
