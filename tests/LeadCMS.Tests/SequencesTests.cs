// <copyright file="SequencesTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.Sequences.Interfaces;
using LeadCMS.Data;
using LeadCMS.Helpers;
using LeadCMS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Tests;

public class SequencesTests : BaseTestAutoLogin
{
    private const string SequencesUrl = "/api/sequences";
    private const string ContactsUrl = "/api/contacts";
    private const string EmailGroupsUrl = "/api/email-groups";
    private const string EmailTemplatesUrl = "/api/email-templates";
    private const string SegmentsUrl = "/api/segments";
    private const string TasksUrl = "/api/tasks";

    public SequencesTests()
        : base()
    {
        TrackEntityType<Sequence>();
        TrackEntityType<SequenceStep>();
        TrackEntityType<SequenceEnrollment>();
        TrackEntityType<SequenceDelivery>();
        TrackEntityType<Contact>();
        TrackEntityType<EmailGroup>();
        TrackEntityType<EmailTemplate>();
        TrackEntityType<Segment>();
        TrackEntityType<EmailLog>();
    }

    // ──────────────────────────────────────────────────
    // Sequence CRUD Tests
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateSequence_ReturnsDraftStatus()
    {
        var sequence = new TestSequence("1");
        var location = await PostTest(SequencesUrl, sequence);

        var created = await GetTest<SequenceDetailsDto>(location);
        created.Should().NotBeNull();
        created!.Name.Should().Be(sequence.Name);
        created.Status.Should().Be(SequenceStatus.Draft);
        created.Description.Should().Be(sequence.Description);
        created.Language.Should().Be(sequence.Language);
    }

    [Fact]
    public async Task CreateSequence_WithoutLanguage_Returns422()
    {
        var sequence = new TestSequence("missing-language")
        {
            Language = string.Empty,
        };

        await PostTest<SequenceDetailsDto>(SequencesUrl, sequence, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GetSequence_ReturnsCorrectData()
    {
        var location = await PostTest(SequencesUrl, new TestSequence("get"));
        var sequence = await GetTest<SequenceDetailsDto>(location);

        sequence.Should().NotBeNull();
        sequence!.Id.Should().BeGreaterThan(0);
        sequence.Status.Should().Be(SequenceStatus.Draft);
        sequence.ActiveEnrollmentCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateSequence_DraftAllowsAllFields()
    {
        var location = await PostTest(SequencesUrl, new TestSequence("patch"));
        var id = ExtractId(location);

        var update = new { Name = "UpdatedName", Description = "Updated description", Language = "lv", StopOnReply = true };
        await PatchTest($"{SequencesUrl}/{id}", update);

        var updated = await GetTest<SequenceDetailsDto>($"{SequencesUrl}/{id}");
        updated!.Name.Should().Be("UpdatedName");
        updated.Description.Should().Be("Updated description");
        updated.Language.Should().Be("lv");
        updated.StopOnReply.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateSequence_ClearingLanguage_Returns422()
    {
        var location = await PostTest(SequencesUrl, new TestSequence("patch-language-required"));
        var id = ExtractId(location);

        await PatchTest($"{SequencesUrl}/{id}", new { Language = " " }, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task DeleteSequence_ReturnsNoContent()
    {
        var location = await PostTest(SequencesUrl, new TestSequence("del"));
        var id = ExtractId(location);

        await DeleteTest($"{SequencesUrl}/{id}");

        await GetTest<SequenceDetailsDto>($"{SequencesUrl}/{id}", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListSequences_ReturnsAll()
    {
        await PostTest(SequencesUrl, new TestSequence("list1"));
        await PostTest(SequencesUrl, new TestSequence("list2"));

        var sequences = await GetTest<List<SequenceDetailsDto>>(SequencesUrl);
        sequences.Should().NotBeNull();
        sequences!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    // ──────────────────────────────────────────────────
    // Lifecycle Transition Tests
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task ActivateSequence_WithSteps_TransitionsToActive()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("activate");

        var result = await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Status.Should().Be(SequenceStatus.Active);
        result.LastActivatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ActivateSequence_WithNoSteps_Returns422()
    {
        var location = await PostTest(SequencesUrl, new TestSequence("no-steps"));
        var id = ExtractId(location);

        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{id}/activate", new { }, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task PauseSequence_TransitionsToPaused()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("pause");
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var result = await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/pause", new { }, HttpStatusCode.OK);
        result!.Status.Should().Be(SequenceStatus.Paused);
        result.LastPausedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task PauseSequence_WhenNotActive_Returns422()
    {
        var location = await PostTest(SequencesUrl, new TestSequence("pause-draft"));
        var id = ExtractId(location);

        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{id}/pause", new { }, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task ArchiveSequence_TransitionsToArchived()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("archive");
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var result = await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/archive", new { }, HttpStatusCode.OK);
        result!.Status.Should().Be(SequenceStatus.Archived);
        result.ArchivedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ArchiveSequence_ExitsActiveEnrollments()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("archive-enroll", delayMinutes: 1440);
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        // Enroll a contact
        var contactId = await CreateContactAsync("archive-enroll");
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        // Archive
        var result = await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/archive", new { }, HttpStatusCode.OK);
        result!.ActiveEnrollmentCount.Should().Be(0);
        result.ExitedEnrollmentCount.Should().Be(1);
    }

    [Fact]
    public async Task ReactivateSequence_FromPaused()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("reactivate");
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/pause", new { }, HttpStatusCode.OK);

        var result = await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);
        result!.Status.Should().Be(SequenceStatus.Active);
    }

    [Fact]
    public async Task UpdateSequence_WhenActive_Returns422()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("edit-active");
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        await PatchTest($"{SequencesUrl}/{sequenceId}", new { Name = "ShouldFail" }, HttpStatusCode.UnprocessableEntity);
    }

    // ──────────────────────────────────────────────────
    // Enrollment Tests
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task EnrollContact_InActiveSequence_Succeeds()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("enroll", delayMinutes: 1440);
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("enroll");
        var result = await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId }, EnrollmentReason = "Test enrollment" },
            HttpStatusCode.Created);

        result.Should().NotBeNull();
        result!.Count.Should().Be(1);
        result[0].Status.Should().Be(SequenceEnrollmentStatus.Active);
        result[0].EnrollmentSource.Should().Be(SequenceEnrollmentSource.Manual);
    }

    [Fact]
    public async Task EnrollContact_InDraftSequence_Returns422()
    {
        var location = await PostTest(SequencesUrl, new TestSequence("enroll-draft"));
        var sequenceId = ExtractId(location);
        var contactId = await CreateContactAsync("enroll-draft");

        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task EnrollContact_DuplicateWithOnceEver_Returns422()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("dup-enroll");
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("dup-enroll");

        // First enrollment succeeds
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        // Second enrollment fails (reentry policy is OnceEver)
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task EnrollContact_AllowAfterCompletion_ReenrollsAfterCompleted()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("reentry-aac");

        // Set reentry policy to AllowAfterCompletion
        await PatchTest($"{SequencesUrl}/{sequenceId}", new
        {
            Enrollment = new SequenceEnrollmentConfig
            {
                Modes = new[] { "manual", "api" },
                ReentryPolicy = ReentryPolicy.AllowAfterCompletion,
            },
        });

        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("reentry-aac");

        // First enrollment
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        // Run task to schedule + send + complete (single 0-delay step)
        await ExecuteSequenceSendTask();
        await ExecuteSequenceSendTask();

        // Verify first enrollment completed
        var completedEnrollments = await GetTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments?filter[where][Status]=Completed");
        completedEnrollments!.Count.Should().Be(1);

        // Re-enrollment after completion should succeed
        var reEnrollments = await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);
        reEnrollments!.Count.Should().Be(1);

        // Run task again — new enrollment gets its own delivery
        await ExecuteSequenceSendTask();
        await ExecuteSequenceSendTask();

        var stats = await GetTest<SequenceStatisticsDto>($"{SequencesUrl}/{sequenceId}/statistics");
        stats!.SentCount.Should().Be(2);
        stats.CompletedEnrollmentCount.Should().Be(2);
    }

    [Fact]
    public async Task EnrollContact_AllowAfterCompletion_RejectsWhileActive()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("reentry-aac-active", delayMinutes: 1440);

        await PatchTest($"{SequencesUrl}/{sequenceId}", new
        {
            Enrollment = new SequenceEnrollmentConfig
            {
                Modes = new[] { "manual", "api" },
                ReentryPolicy = ReentryPolicy.AllowAfterCompletion,
            },
        });

        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("reentry-aac-active");

        // First enrollment
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        // Re-enrollment while active should fail
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task EnrollContact_AllowAfterCompletion_ReenrollsAfterExited()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("reentry-aac-exited");

        await PatchTest($"{SequencesUrl}/{sequenceId}", new
        {
            Enrollment = new SequenceEnrollmentConfig
            {
                Modes = new[] { "manual", "api" },
                ReentryPolicy = ReentryPolicy.AllowAfterCompletion,
            },
        });

        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("reentry-aac-exited");
        await UnsubscribeContactAsync(contactId);

        var firstEnrollments = await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);
        firstEnrollments.Should().NotBeNull();
        firstEnrollments!.Should().ContainSingle().Which.Status.Should().Be(SequenceEnrollmentStatus.Exited);

        var secondEnrollments = await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);
        secondEnrollments.Should().NotBeNull();
        secondEnrollments!.Should().ContainSingle().Which.Status.Should().Be(SequenceEnrollmentStatus.Exited);

        var exitedEnrollments = await GetTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments?filter[where][Status]=Exited");
        exitedEnrollments.Should().NotBeNull();
        exitedEnrollments!.Count.Should().Be(2);
        exitedEnrollments.Should().OnlyContain(e => e.ExitReason == SequenceExitReason.Unsubscribed);
    }

    [Fact]
    public async Task EnrollContact_Always_ReenrollsAfterExited()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("reentry-always", delayMinutes: 1440);

        await PatchTest($"{SequencesUrl}/{sequenceId}", new
        {
            Enrollment = new SequenceEnrollmentConfig
            {
                Modes = new[] { "manual", "api" },
                ReentryPolicy = ReentryPolicy.Always,
            },
        });

        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("reentry-always");

        // First enrollment
        var enrollments = await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        // Remove enrollment (exit)
        await DeleteTest(
            $"{SequencesUrl}/{sequenceId}/enrollments/{enrollments![0].Id}",
            HttpStatusCode.OK);

        // Re-enrollment after exit should succeed
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        // Run task — new enrollment gets its own delivery
        await ExecuteSequenceSendTask();
        await ExecuteSequenceSendTask();

        var stats = await GetTest<SequenceStatisticsDto>($"{SequencesUrl}/{sequenceId}/statistics");
        stats!.SentCount.Should().Be(0);
    }

    [Fact]
    public async Task EnrollContact_Always_AllowsConcurrentActiveEnrollments()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("reentry-always-active", delayMinutes: 1440);

        await PatchTest($"{SequencesUrl}/{sequenceId}", new
        {
            Enrollment = new SequenceEnrollmentConfig
            {
                Modes = new[] { "manual", "api" },
                ReentryPolicy = ReentryPolicy.Always,
            },
        });

        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("reentry-always-active");

        // First enrollment
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        // Re-enrollment while active should succeed
        var secondEnrollment = await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);
        secondEnrollment.Should().NotBeNull();

        // Both enrollments should progress independently
        await ExecuteSequenceSendTask();
        await ExecuteSequenceSendTask();

        using (var scope = App.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();
            var deliveries = await dbContext.SequenceDeliveries!
                .Where(d => d.SequenceId == sequenceId && d.ContactId == contactId)
                .ToListAsync();

            // Each enrollment should get its own scheduled delivery
            deliveries.Should().HaveCount(2);
        }
    }

    [Fact]
    public async Task EnrollContact_WithTemplateArguments_PersistsArguments()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("tmpl-args", delayMinutes: 1440);
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("tmpl-args");

        var templateArgs = new Dictionary<string, string>
        {
            { "CompanyName", "Acme Corp" },
            { "TrialDays", "14" },
        };

        var result = await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto
            {
                ContactIds = new[] { contactId },
                EnrollmentReason = "Test with args",
                TemplateArguments = templateArgs,
            },
            HttpStatusCode.Created);

        result.Should().NotBeNull();
        result!.Count.Should().Be(1);
        result[0].TemplateArguments.Should().NotBeNull();
        result[0].TemplateArguments.Should().ContainKey("CompanyName").WhoseValue.Should().Be("Acme Corp");
        result[0].TemplateArguments.Should().ContainKey("TrialDays").WhoseValue.Should().Be("14");
    }

    [Fact]
    public async Task RemoveEnrollment_SetsExitedStatus()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("remove-enroll", delayMinutes: 1440);
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("remove-enroll");
        var enrollments = await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        var result = await DeleteTest(
            $"{SequencesUrl}/{sequenceId}/enrollments/{enrollments![0].Id}",
            HttpStatusCode.OK);

        var content = await result.Content.ReadAsStringAsync();
        var enrollment = JsonHelper.Deserialize<SequenceEnrollmentDetailsDto>(content);
        enrollment!.Status.Should().Be(SequenceEnrollmentStatus.Exited);
        enrollment.ExitReason.Should().Be(SequenceExitReason.ManuallyRemoved);
    }

    [Fact]
    public async Task StopEnrollments_SetsExitedStatusWithReason()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("stop-enroll", delayMinutes: 1440);
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contact1 = await CreateContactAsync("stop-enroll1");
        var contact2 = await CreateContactAsync("stop-enroll2");
        var enrollments = await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contact1, contact2 } },
            HttpStatusCode.Created);

        var stopDto = new SequenceEnrollmentStopDto
        {
            EnrollmentIds = new[] { enrollments![0].Id, enrollments[1].Id },
        };

        var result = await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments/stop",
            stopDto,
            HttpStatusCode.OK);

        result.Should().NotBeNull();
        result!.Count.Should().Be(2);
        result.Should().AllSatisfy(e =>
        {
            e.Status.Should().Be(SequenceEnrollmentStatus.Exited);
            e.ExitReason.Should().Be(SequenceExitReason.ManuallyRemoved);
            e.ExitedAt.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task StopEnrollments_SkipsNonActiveEnrollments()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("stop-skip", delayMinutes: 1440);
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contact1 = await CreateContactAsync("stop-skip1");
        var contact2 = await CreateContactAsync("stop-skip2");
        var enrollments = await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contact1, contact2 } },
            HttpStatusCode.Created);

        // Remove first enrollment so it's already exited
        await DeleteTest(
            $"{SequencesUrl}/{sequenceId}/enrollments/{enrollments![0].Id}",
            HttpStatusCode.OK);

        var stopDto = new SequenceEnrollmentStopDto
        {
            EnrollmentIds = new[] { enrollments[0].Id, enrollments[1].Id },
        };

        var result = await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments/stop",
            stopDto,
            HttpStatusCode.OK);

        // Only the second active enrollment should be stopped
        result.Should().NotBeNull();
        result!.Count.Should().Be(1);
        result[0].ContactId.Should().Be(contact2);
    }

    [Fact]
    public async Task ListEnrollments_FiltersByStatus()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("list-enroll", delayMinutes: 1440);
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("list-enroll");
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        var activeEnrollments = await GetTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments?filter[where][Status]=Active");
        activeEnrollments.Should().NotBeNull();
        activeEnrollments!.Count.Should().Be(1);

        var completedEnrollments = await GetTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments?filter[where][Status]=Completed");
        completedEnrollments!.Count.Should().Be(0);
    }

    // ──────────────────────────────────────────────────
    // Statistics Tests
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task GetStatistics_ReturnsCorrectCounts()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("stats", delayMinutes: 1440);
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contact1 = await CreateContactAsync("stats1");
        var contact2 = await CreateContactAsync("stats2");
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contact1, contact2 } },
            HttpStatusCode.Created);

        var stats = await GetTest<SequenceStatisticsDto>($"{SequencesUrl}/{sequenceId}/statistics");
        stats.Should().NotBeNull();
        stats!.ActiveEnrollmentCount.Should().Be(2);
        stats.StepsCount.Should().Be(1);
    }

    // ──────────────────────────────────────────────────
    // Enrolled Contacts Tests
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task ListEnrolledContacts_ReturnsDistinctContacts()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("enrolled-contacts", delayMinutes: 1440);
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contact1 = await CreateContactAsync("enrolled-c1");
        var contact2 = await CreateContactAsync("enrolled-c2");
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contact1, contact2 } },
            HttpStatusCode.Created);

        var contacts = await GetTest<List<ContactDetailsDto>>($"{SequencesUrl}/{sequenceId}/contacts");
        contacts.Should().NotBeNull();
        contacts!.Count.Should().Be(2);
        contacts.Select(c => c.Id).Should().Contain(contact1);
        contacts.Select(c => c.Id).Should().Contain(contact2);
    }

    [Fact]
    public async Task ListEnrolledContacts_NoEnrollments_ReturnsEmpty()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("no-enrolled");

        var contacts = await GetTest<List<ContactDetailsDto>>($"{SequencesUrl}/{sequenceId}/contacts");
        contacts.Should().NotBeNull();
        contacts!.Count.Should().Be(0);
    }

    [Fact]
    public async Task ListEnrolledContacts_MultipleEnrollmentsSameContact_ReturnsOnce()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("dedup-contacts");
        await PatchTest($"{SequencesUrl}/{sequenceId}", new { Enrollment = new SequenceEnrollmentConfig { Modes = new[] { "manual" }, ReentryPolicy = ReentryPolicy.Always } });
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("dedup-c");
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        var contacts = await GetTest<List<ContactDetailsDto>>($"{SequencesUrl}/{sequenceId}/contacts");
        contacts.Should().NotBeNull();
        contacts!.Count.Should().Be(1);
        contacts[0].Id.Should().Be(contactId);
    }

    [Fact]
    public async Task ListEnrollments_WithIncludedContact_PopulatesAvatarUrl()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("enrollment-contact-avatar", delayMinutes: 1440);
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contact = new TestContact("enrollment-contact-avatar");
        var contactId = ExtractId(await PostTest(ContactsUrl, contact));

        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        var enrollments = await GetTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments?filter[include]=Contact");

        enrollments.Should().NotBeNull();
        var enrollment = enrollments!.Should().ContainSingle().Subject;
        enrollment.Contact.Should().NotBeNull();
        var includedContact = enrollment.Contact!;
        includedContact.Id.Should().Be(contactId);
        includedContact.AvatarUrl.Should().Be(GravatarHelper.EmailToGravatarUrl(contact.Email));
    }

    // ──────────────────────────────────────────────────
    // Composite API Tests
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_CreatesSequenceWithSteps()
    {
        var templateId = await CreateEmailTemplateAsync("post-steps");

        var createDto = new SequenceCreateDto
        {
            Name = "PostWithSteps",
            Description = "Created with steps in one call",
            Language = "en",
            StopOnReply = true,
            Enrollment = new SequenceEnrollmentConfig
            {
                Modes = new[] { "manual", "api" },
                ReentryPolicy = ReentryPolicy.OnceEver,
            },
            Steps = new List<SequenceStepCreateDto>
            {
                new() { Name = "welcome", EmailTemplateId = templateId, Timing = new SequenceStepTiming { Delay = new SequenceStepDelay { Value = 0, Unit = "minutes" } } },
                new() { Name = "follow-up", EmailTemplateId = templateId, Timing = new SequenceStepTiming { Delay = new SequenceStepDelay { Value = 1, Unit = "days" } } },
            },
        };

        var created = await PostTest<SequenceDetailsDto>(SequencesUrl, createDto, HttpStatusCode.Created);

        created.Should().NotBeNull();
        created!.Name.Should().Be("PostWithSteps");
        created.Status.Should().Be(SequenceStatus.Draft);
        created.StopOnReply.Should().BeTrue();
        created.Steps.Should().HaveCount(2);
        created.Steps[0].Name.Should().Be("welcome");
        created.Steps[1].Name.Should().Be("follow-up");
    }

    [Fact]
    public async Task GetOne_ReturnsSequenceWithSteps()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("get-one-steps");

        var result = await GetTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}");

        result.Should().NotBeNull();
        result!.Id.Should().Be(sequenceId);
        result.Steps.Should().HaveCount(1);
    }

    [Fact]
    public async Task Put_UpdatesSequenceAndSteps()
    {
        var templateId = await CreateEmailTemplateAsync("put-steps");

        // Create via POST
        var createDto = new SequenceCreateDto
        {
            Name = "OriginalName",
            Language = "en",
            Steps = new List<SequenceStepCreateDto>
            {
                new() { Name = "step-a", EmailTemplateId = templateId, Timing = new SequenceStepTiming { Delay = new SequenceStepDelay { Value = 0, Unit = "minutes" } } },
                new() { Name = "step-b", EmailTemplateId = templateId, Timing = new SequenceStepTiming { Delay = new SequenceStepDelay { Value = 1, Unit = "days" } } },
            },
        };

        var created = await PostTest<SequenceDetailsDto>(SequencesUrl, createDto, HttpStatusCode.Created);
        var sequenceId = created!.Id;

        // Update: rename, remove step-a, add step-c, keep step-b
        var updateDto = new SequenceCreateDto
        {
            Name = "UpdatedName",
            Language = "fr",
            Steps = new List<SequenceStepCreateDto>
            {
                new() { Name = "step-b", EmailTemplateId = templateId, Timing = new SequenceStepTiming { Delay = new SequenceStepDelay { Value = 2, Unit = "days" } } },
                new() { Name = "step-c", EmailTemplateId = templateId, Timing = new SequenceStepTiming { Delay = new SequenceStepDelay { Value = 3, Unit = "days" } } },
            },
        };

        var putResponse = await Request(HttpMethod.Put, $"{SequencesUrl}/{sequenceId}", updateDto);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = JsonHelper.Deserialize<SequenceDetailsDto>(await putResponse.Content.ReadAsStringAsync());

        updated.Should().NotBeNull();
        updated!.Name.Should().Be("UpdatedName");
        updated.Language.Should().Be("fr");
        updated.Steps.Should().HaveCount(2);
        updated.Steps[0].Name.Should().Be("step-b");
        updated.Steps[0].Timing.Delay.Value.Should().Be(2);
        updated.Steps[1].Name.Should().Be("step-c");
    }

    [Fact]
    public async Task Put_WhenActive_Returns422()
    {
        var templateId = await CreateEmailTemplateAsync("put-active");

        var createDto = new SequenceCreateDto
        {
            Name = "ActiveSeq",
            Language = "en",
            Enrollment = new SequenceEnrollmentConfig { Modes = new[] { "manual", "api" } },
            Steps = new List<SequenceStepCreateDto>
            {
                new() { Name = "s1", EmailTemplateId = templateId, Timing = new SequenceStepTiming { Delay = new SequenceStepDelay { Value = 0, Unit = "minutes" } } },
            },
        };

        var created = await PostTest<SequenceDetailsDto>(SequencesUrl, createDto, HttpStatusCode.Created);

        // Activate
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{created!.Id}/activate", new { }, HttpStatusCode.OK);

        // PUT on active sequence should fail
        var putResponse = await Request(HttpMethod.Put, $"{SequencesUrl}/{created.Id}", createDto);
        putResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ──────────────────────────────────────────────────
    // Immediate Step Processing Tests
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Enroll_ImmediateStep_SentWithoutTaskExecution()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("imm-send");
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("imm-send");
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        // Without executing the task, enrollment should already be completed
        var enrollments = await GetTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments?filter[where][Status]=Completed");
        enrollments!.Count.Should().Be(1);

        var stats = await GetTest<SequenceStatisticsDto>($"{SequencesUrl}/{sequenceId}/statistics");
        stats!.SentCount.Should().Be(1);
        stats.CompletedEnrollmentCount.Should().Be(1);
        stats.ActiveEnrollmentCount.Should().Be(0);
    }

    [Fact]
    public async Task Enroll_ImmediateMultiStep_AllSentWithoutTaskExecution()
    {
        var templateId = await CreateEmailTemplateAsync("imm-multi");
        var createDto = new SequenceCreateDto
        {
            Name = "ImmMultiStep",
            Language = "en",
            Enrollment = new SequenceEnrollmentConfig { Modes = new[] { "manual", "api" } },
            Steps = new List<SequenceStepCreateDto>
            {
                new() { Name = "imm1", EmailTemplateId = templateId, Timing = new SequenceStepTiming { Delay = new SequenceStepDelay { Value = 0, Unit = "minutes" } } },
                new() { Name = "imm2", EmailTemplateId = templateId, Timing = new SequenceStepTiming { Delay = new SequenceStepDelay { Value = 0, Unit = "minutes" } } },
            },
        };

        var created = await PostTest<SequenceDetailsDto>(SequencesUrl, createDto, HttpStatusCode.Created);
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{created!.Id}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("imm-multi");
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{created.Id}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        // Both immediate steps should be sent without any task execution
        var stats = await GetTest<SequenceStatisticsDto>($"{SequencesUrl}/{created.Id}/statistics");
        stats!.SentCount.Should().Be(2);
        stats.CompletedEnrollmentCount.Should().Be(1);
        stats.ActiveEnrollmentCount.Should().Be(0);
    }

    [Fact]
    public async Task Enroll_MixedImmediateAndDelayed_OnlyImmediateSent()
    {
        var templateId = await CreateEmailTemplateAsync("imm-mixed");
        var createDto = new SequenceCreateDto
        {
            Name = "MixedSteps",
            Language = "en",
            Enrollment = new SequenceEnrollmentConfig { Modes = new[] { "manual", "api" } },
            Steps = new List<SequenceStepCreateDto>
            {
                new() { Name = "imm", EmailTemplateId = templateId, Timing = new SequenceStepTiming { Delay = new SequenceStepDelay { Value = 0, Unit = "minutes" } } },
                new() { Name = "delayed", EmailTemplateId = templateId, Timing = new SequenceStepTiming { Delay = new SequenceStepDelay { Value = 1, Unit = "days" } } },
            },
        };

        var created = await PostTest<SequenceDetailsDto>(SequencesUrl, createDto, HttpStatusCode.Created);
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{created!.Id}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("imm-mixed");
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{created.Id}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        // Only the immediate step should be sent; enrollment still active
        var stats = await GetTest<SequenceStatisticsDto>($"{SequencesUrl}/{created.Id}/statistics");
        stats!.SentCount.Should().Be(1);
        stats.ActiveEnrollmentCount.Should().Be(1);
        stats.CompletedEnrollmentCount.Should().Be(0);

        // The delayed step should already be scheduled without needing a task run
        var scheduled = await GetTest<List<SequenceDeliveryDetailsDto>>(
            $"{DeliveriesUrl(created.Id)}?filter[where][Status]=Scheduled");
        scheduled!.Count.Should().Be(1, "next delayed step should be scheduled immediately after enrollment");
    }

    [Fact]
    public async Task Enroll_UnsubscribedContact_ExitsBeforeCreatingDeliveries()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("unsubscribed-before-send");
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("unsubscribed-before-send");
        await UnsubscribeContactAsync(contactId);

        var created = await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        created.Should().NotBeNull();
        var enrollment = created!.Should().ContainSingle().Subject;
        enrollment.ContactId.Should().Be(contactId);
        enrollment.Status.Should().Be(SequenceEnrollmentStatus.Exited);
        enrollment.ExitReason.Should().Be(SequenceExitReason.Unsubscribed);
        enrollment.ExitedAt.Should().NotBeNull();

        var deliveries = await GetTest<List<SequenceDeliveryDetailsDto>>(DeliveriesUrl(sequenceId));
        deliveries.Should().NotBeNull();
        deliveries!.Should().BeEmpty();

        var stats = await GetTest<SequenceStatisticsDto>($"{SequencesUrl}/{sequenceId}/statistics");
        stats.Should().NotBeNull();
        stats!.SentCount.Should().Be(0);
        stats.ActiveEnrollmentCount.Should().Be(0);
        stats.ExitedEnrollmentCount.Should().Be(1);
    }

    // ──────────────────────────────────────────────────
    // Delivery API Tests
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task ListDeliveries_ReturnsDeliveriesForSequence()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("list-del");
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("list-del");
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        // Immediate step creates a delivery
        var deliveries = await GetTest<List<SequenceDeliveryDetailsDto>>(DeliveriesUrl(sequenceId));
        deliveries.Should().NotBeNull();
        deliveries!.Count.Should().BeGreaterThanOrEqualTo(1);
        deliveries[0].SequenceId.Should().Be(sequenceId);
        deliveries[0].ContactId.Should().Be(contactId);
    }

    [Fact]
    public async Task ListDeliveries_FiltersByStatus()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("del-filter");
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("del-filter");
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        // Immediate step sends, so delivery should be Sent
        var sent = await GetTest<List<SequenceDeliveryDetailsDto>>($"{DeliveriesUrl(sequenceId)}?filter[where][Status]=Sent");
        sent.Should().NotBeNull();
        sent!.Count.Should().BeGreaterThanOrEqualTo(1);

        var scheduled = await GetTest<List<SequenceDeliveryDetailsDto>>($"{DeliveriesUrl(sequenceId)}?filter[where][Status]=Scheduled");
        scheduled.Should().NotBeNull();
        scheduled!.Count.Should().Be(0);
    }

    [Fact]
    public async Task GetOneDelivery_ReturnsDelivery()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("get-del");
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("get-del");
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        var deliveries = await GetTest<List<SequenceDeliveryDetailsDto>>(DeliveriesUrl(sequenceId));
        deliveries!.Count.Should().BeGreaterThanOrEqualTo(1);

        var single = await GetTest<SequenceDeliveryDetailsDto>($"{DeliveriesUrl(sequenceId)}/{deliveries[0].Id}");
        single.Should().NotBeNull();
        single!.Id.Should().Be(deliveries[0].Id);
        single.ContactId.Should().Be(contactId);
    }

    [Fact]
    public async Task GetOneDelivery_NotFound_Returns404()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("del-404", delayMinutes: 1440);
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        await GetTest<SequenceDeliveryDetailsDto>($"{DeliveriesUrl(sequenceId)}/99999", HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────────
    // Task Execution Tests
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task SequenceSendTask_SchedulesAndSendsDelivery()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("task-send");
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("task-send");
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        // Execute the task twice: first to schedule, second to send
        await ExecuteSequenceSendTask();
        await ExecuteSequenceSendTask();

        // Verify the enrollment was completed (single step with 0-minute delay)
        var enrollments = await GetTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments?filter[where][Status]=Completed");
        enrollments!.Count.Should().Be(1);

        // Verify statistics updated
        var stats = await GetTest<SequenceStatisticsDto>($"{SequencesUrl}/{sequenceId}/statistics");
        stats!.SentCount.Should().Be(1);
        stats.CompletedEnrollmentCount.Should().Be(1);
        stats.ActiveEnrollmentCount.Should().Be(0);
    }

    [Fact]
    public async Task SequenceSendTask_MultiStepSequence_ProcessesInOrder()
    {
        var templateId = await CreateEmailTemplateAsync("multi-step");
        var sequence = new TestSequence("multi-step");
        sequence.Steps = new List<SequenceStepCreateDto>
        {
            new() { Name = "ms1", EmailTemplateId = templateId, Timing = new SequenceStepTiming { Delay = new SequenceStepDelay { Value = 0, Unit = "minutes" } } },
            new() { Name = "ms2", EmailTemplateId = templateId, Timing = new SequenceStepTiming { Delay = new SequenceStepDelay { Value = 0, Unit = "minutes" } } },
        };
        var created = await PostTest<SequenceDetailsDto>(SequencesUrl, sequence, HttpStatusCode.Created);
        var sequenceId = created!.Id;

        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("multi-step");
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        // A single task execution should schedule and send all immediate steps
        await ExecuteSequenceSendTask();

        var completedEnrollments = await GetTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments?filter[where][Status]=Completed");
        completedEnrollments!.Count.Should().Be(1);

        var stats = await GetTest<SequenceStatisticsDto>($"{SequencesUrl}/{sequenceId}/statistics");
        stats!.SentCount.Should().Be(2);
    }

    [Fact]
    public async Task SequenceSendTask_DuplicateEnrollments_DoNotResendAfterCompletion()
    {
        var templateId = await CreateEmailTemplateAsync("dup-no-resend");
        var sequence = new TestSequence("dup-no-resend");
        sequence.Steps = new List<SequenceStepCreateDto>
        {
            new() { Name = "step1", EmailTemplateId = templateId, Timing = new SequenceStepTiming { Delay = new SequenceStepDelay { Value = 0, Unit = "minutes" } } },
            new() { Name = "step2", EmailTemplateId = templateId, Timing = new SequenceStepTiming { Delay = new SequenceStepDelay { Value = 0, Unit = "minutes" } } },
        };
        var created = await PostTest<SequenceDetailsDto>(SequencesUrl, sequence, HttpStatusCode.Created);
        var sequenceId = created!.Id;

        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("dup-no-resend");

        // Create two active enrollments (simulates migrated data)
        using (var scope = App.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();
            await dbContext.SequenceEnrollments!.AddRangeAsync(
                new SequenceEnrollment
                {
                    SequenceId = sequenceId,
                    ContactId = contactId,
                    Status = SequenceEnrollmentStatus.Active,
                    EnteredAt = DateTime.UtcNow.AddMinutes(-20),
                    EnrollmentSource = SequenceEnrollmentSource.Migration,
                    EnrollmentReason = "older-enrollment",
                },
                new SequenceEnrollment
                {
                    SequenceId = sequenceId,
                    ContactId = contactId,
                    Status = SequenceEnrollmentStatus.Active,
                    EnteredAt = DateTime.UtcNow.AddMinutes(-10),
                    EnrollmentSource = SequenceEnrollmentSource.Migration,
                    EnrollmentReason = "newer-enrollment",
                });
            await dbContext.SaveChangesAsync();
        }

        // Run task many times to ensure no infinite re-sends
        for (int i = 0; i < 10; i++)
        {
            await ExecuteSequenceSendTask();
        }

        using (var scope = App.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();

            var sentDeliveries = await dbContext.SequenceDeliveries!
                .Where(d => d.SequenceId == sequenceId && d.ContactId == contactId && d.Status == SequenceDeliveryStatus.Sent)
                .ToListAsync();

            // Should have exactly 4 sent deliveries (2 enrollments × 2 steps), not more
            sentDeliveries.Should().HaveCount(4, "each enrollment should complete independently through all steps");

            var activeEnrollments = await dbContext.SequenceEnrollments!
                .Where(e => e.SequenceId == sequenceId && e.ContactId == contactId && e.Status == SequenceEnrollmentStatus.Active)
                .ToListAsync();

            activeEnrollments.Should().BeEmpty("no active enrollments should remain after completion");
        }
    }

    [Fact]
    public async Task SequenceSendTask_SentDelivery_HasEmailLogId()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("email-log-link");
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("email-log-link");
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        await ExecuteSequenceSendTask();
        await ExecuteSequenceSendTask();

        using (var scope = App.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();

            var sentDeliveries = await dbContext.SequenceDeliveries!
                .Where(d => d.SequenceId == sequenceId && d.ContactId == contactId && d.Status == SequenceDeliveryStatus.Sent)
                .ToListAsync();

            sentDeliveries.Should().NotBeEmpty();
            foreach (var delivery in sentDeliveries)
            {
                delivery.EmailLogId.Should().NotBeNull("every sent delivery should reference its email_log");
                delivery.EmailLogId.Should().BeGreaterThan(0);
            }
        }
    }

    [Theory]
    [InlineData(ReentryPolicy.AllowAfterCompletion)]
    [InlineData(ReentryPolicy.Always)]
    public async Task SequenceSendTask_SegmentEnrollment_DoesNotAutomaticallyReenrollCompletedContacts(ReentryPolicy reentryPolicy)
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync($"segment-once-{reentryPolicy}");
        var contactId = await CreateContactAsync($"segment-once-{reentryPolicy}");
        var includeSegmentId = await CreateStaticSegmentAsync($"segment-once-{reentryPolicy}", new[] { contactId });

        await PatchTest($"{SequencesUrl}/{sequenceId}", new
        {
            Enrollment = new SequenceEnrollmentConfig
            {
                Modes = new[] { "segment", "manual", "api" },
                IncludeSegmentIds = new[] { includeSegmentId },
                ReentryPolicy = reentryPolicy,
            },
        });

        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        await ExecuteSequenceSendTask();
        await ExecuteSequenceSendTask();
        await ExecuteSequenceSendTask();

        var enrollments = await GetTest<List<SequenceEnrollmentDetailsDto>>($"{SequencesUrl}/{sequenceId}/enrollments");
        enrollments.Should().NotBeNull();
        var enrollment = enrollments!.Should().ContainSingle().Subject;
        enrollment.ContactId.Should().Be(contactId);
        enrollment.EnrollmentSource.Should().Be(SequenceEnrollmentSource.Segment);
        enrollment.Status.Should().Be(SequenceEnrollmentStatus.Completed);

        var stats = await GetTest<SequenceStatisticsDto>($"{SequencesUrl}/{sequenceId}/statistics");
        stats.Should().NotBeNull();
        stats!.SentCount.Should().Be(1);
        stats.CompletedEnrollmentCount.Should().Be(1);
        stats.ActiveEnrollmentCount.Should().Be(0);
    }

    [Fact]
    public async Task SequenceSendTask_ExcludeSegment_ExitsActiveEnrollmentWhenContactBecomesExcluded()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("segment-exclude-exit", delayMinutes: 1440);
        var enrolledContactId = await CreateContactAsync("segment-exclude-enrolled");
        var excludedContactId = await CreateContactAsync("segment-exclude-existing");
        var includeSegmentId = await CreateStaticSegmentAsync("segment-exclude-include", new[] { enrolledContactId });
        var excludeSegmentId = await CreateStaticSegmentAsync("segment-exclude-exclude", new[] { excludedContactId });

        await PatchTest($"{SequencesUrl}/{sequenceId}", new
        {
            Enrollment = new SequenceEnrollmentConfig
            {
                Modes = new[] { "segment" },
                IncludeSegmentIds = new[] { includeSegmentId },
                ExcludeSegmentIds = new[] { excludeSegmentId },
                ReentryPolicy = ReentryPolicy.OnceEver,
            },
        });

        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        await ExecuteSequenceSendTask();

        var activeEnrollments = await GetTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments?filter[where][Status]=Active");
        activeEnrollments.Should().NotBeNull();
        var activeEnrollment = activeEnrollments!.Should().ContainSingle().Subject;
        activeEnrollment.ContactId.Should().Be(enrolledContactId);

        await PatchTest($"{SegmentsUrl}/{excludeSegmentId}", new { ContactIds = new[] { excludedContactId, enrolledContactId } });

        await ExecuteSequenceSendTask();

        var exitedEnrollments = await GetTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments?filter[where][Status]=Exited");
        exitedEnrollments.Should().NotBeNull();
        var exitedEnrollment = exitedEnrollments!.Should().ContainSingle().Subject;
        exitedEnrollment.ContactId.Should().Be(enrolledContactId);
        exitedEnrollment.ExitReason.Should().Be(SequenceExitReason.ExcludedBySegment);

        var stats = await GetTest<SequenceStatisticsDto>($"{SequencesUrl}/{sequenceId}/statistics");
        stats.Should().NotBeNull();
        stats!.ActiveEnrollmentCount.Should().Be(0);
        stats.ExitedEnrollmentCount.Should().Be(1);
    }

    // ──────────────────────────────────────────────────
    // Cancellation: Scheduled Deliveries Tests
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task RemoveEnrollment_CancelsScheduledDeliveries()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("cancel-del-remove", delayMinutes: 1440);
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("cancel-del-remove");
        var enrollments = await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        // Run task to schedule delivery
        await ExecuteSequenceSendTask();

        // Verify delivery is Scheduled before removal
        var deliveriesBefore = await GetTest<List<SequenceDeliveryDetailsDto>>(
            $"{DeliveriesUrl(sequenceId)}?filter[where][Status]=Scheduled");
        deliveriesBefore!.Count.Should().Be(1);

        // Remove enrollment
        await DeleteTest($"{SequencesUrl}/{sequenceId}/enrollments/{enrollments![0].Id}", HttpStatusCode.OK);

        // Verify scheduled delivery is now Skipped
        var scheduledAfter = await GetTest<List<SequenceDeliveryDetailsDto>>(
            $"{DeliveriesUrl(sequenceId)}?filter[where][Status]=Scheduled");
        scheduledAfter!.Count.Should().Be(0);

        var skippedAfter = await GetTest<List<SequenceDeliveryDetailsDto>>(
            $"{DeliveriesUrl(sequenceId)}?filter[where][Status]=Skipped");
        skippedAfter!.Count.Should().Be(1);
        skippedAfter[0].SkipReason.Should().Be("EnrollmentCancelled");
    }

    [Fact]
    public async Task StopEnrollments_CancelsScheduledDeliveries()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("cancel-del-stop", delayMinutes: 1440);
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contact1 = await CreateContactAsync("cancel-del-stop1");
        var contact2 = await CreateContactAsync("cancel-del-stop2");
        var enrollments = await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contact1, contact2 } },
            HttpStatusCode.Created);

        // Run task to schedule deliveries
        await ExecuteSequenceSendTask();

        var scheduledBefore = await GetTest<List<SequenceDeliveryDetailsDto>>(
            $"{DeliveriesUrl(sequenceId)}?filter[where][Status]=Scheduled");
        scheduledBefore!.Count.Should().Be(2);

        // Stop both enrollments
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments/stop",
            new SequenceEnrollmentStopDto { EnrollmentIds = new[] { enrollments![0].Id, enrollments[1].Id } },
            HttpStatusCode.OK);

        // Verify all scheduled deliveries are now Skipped
        var scheduledAfter = await GetTest<List<SequenceDeliveryDetailsDto>>(
            $"{DeliveriesUrl(sequenceId)}?filter[where][Status]=Scheduled");
        scheduledAfter!.Count.Should().Be(0);

        var skippedAfter = await GetTest<List<SequenceDeliveryDetailsDto>>(
            $"{DeliveriesUrl(sequenceId)}?filter[where][Status]=Skipped");
        skippedAfter!.Count.Should().Be(2);
    }

    [Fact]
    public async Task ArchiveSequence_CancelsScheduledDeliveries()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("cancel-del-archive", delayMinutes: 1440);
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("cancel-del-archive");
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        // Run task to schedule delivery
        await ExecuteSequenceSendTask();

        var scheduledBefore = await GetTest<List<SequenceDeliveryDetailsDto>>(
            $"{DeliveriesUrl(sequenceId)}?filter[where][Status]=Scheduled");
        scheduledBefore!.Count.Should().Be(1);

        // Archive the sequence
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/archive", new { }, HttpStatusCode.OK);

        // Verify scheduled delivery is now Skipped
        var scheduledAfter = await GetTest<List<SequenceDeliveryDetailsDto>>(
            $"{DeliveriesUrl(sequenceId)}?filter[where][Status]=Scheduled");
        scheduledAfter!.Count.Should().Be(0);

        var skippedAfter = await GetTest<List<SequenceDeliveryDetailsDto>>(
            $"{DeliveriesUrl(sequenceId)}?filter[where][Status]=Skipped");
        skippedAfter!.Count.Should().Be(1);
        skippedAfter[0].SkipReason.Should().Be("EnrollmentCancelled");
    }

    [Fact]
    public async Task SequenceSendTask_DoesNotSendDeliveriesForExitedEnrollments()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("no-send-exited", delayMinutes: 1440);
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("no-send-exited");
        var enrollments = await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        // Run task to schedule delivery
        await ExecuteSequenceSendTask();

        // Remove the enrollment (this should cancel scheduled deliveries)
        await DeleteTest($"{SequencesUrl}/{sequenceId}/enrollments/{enrollments![0].Id}", HttpStatusCode.OK);

        // Run task again — should NOT send anything
        await ExecuteSequenceSendTask();

        var stats = await GetTest<SequenceStatisticsDto>($"{SequencesUrl}/{sequenceId}/statistics");
        stats!.SentCount.Should().Be(0);
    }

    [Fact]
    public async Task Enroll_ImmediateStep_TaskDoesNotDuplicateSend()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("no-dup-send");
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("no-dup-send");
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        // Run task multiple times after enrollment — should not duplicate
        await ExecuteSequenceSendTask();
        await ExecuteSequenceSendTask();
        await ExecuteSequenceSendTask();

        var stats = await GetTest<SequenceStatisticsDto>($"{SequencesUrl}/{sequenceId}/statistics");
        stats!.SentCount.Should().Be(1, "immediate step should be sent exactly once");
        stats.CompletedEnrollmentCount.Should().Be(1);
    }

    [Fact]
    public async Task DeleteEmailTemplate_UsedInSequenceStep_Fails()
    {
        var (_, templateId) = await CreateSequenceWithStepAsync("del-guard");

        await DeleteTest($"{EmailTemplatesUrl}/{templateId}", HttpStatusCode.UnprocessableEntity);
    }

    // ──────────────────────────────────────────────────
    // Sync Tests
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Sync_WithIncludeSteps_ReturnsSteps()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("sync-inc");

        var response = await GetRequest($"{SequencesUrl}/sync?filter[include]=Steps");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var syncResponse = JsonHelper.Deserialize<SyncResponseDto<SequenceDetailsDto, int>>(content);

        syncResponse.Should().NotBeNull();
        var sequence = syncResponse!.Items.FirstOrDefault(i => i.Id == sequenceId);
        sequence.Should().NotBeNull();
        sequence!.Steps.Should().NotBeNullOrEmpty();
        sequence.Steps.Count.Should().Be(1);
    }

    // ──────────────────────────────────────────────────
    // TryEnrollContactBySequenceNameAsync Tests
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task TryEnrollByName_ActiveSequence_ReturnsTrue()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("try-enroll", delayMinutes: 1440);
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("try-enroll");

        using var scope = App.Services.CreateScope();
        var sequenceService = scope.ServiceProvider.GetRequiredService<ISequenceService>();

        var result = await sequenceService.TryEnrollContactBySequenceNameAsync(
            "TestSequencetry-enroll",
            new[] { contactId },
            enrollmentReason: "ContactFormSubmission");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryEnrollByName_NonExistentSequence_ReturnsFalse()
    {
        var contactId = await CreateContactAsync("try-noexist");

        using var scope = App.Services.CreateScope();
        var sequenceService = scope.ServiceProvider.GetRequiredService<ISequenceService>();

        var result = await sequenceService.TryEnrollContactBySequenceNameAsync(
            "NonExistentSequence",
            new[] { contactId });

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryEnrollByName_DraftSequence_ReturnsFalse()
    {
        await CreateSequenceWithStepAsync("try-draft", delayMinutes: 1440);
        var contactId = await CreateContactAsync("try-draft");

        using var scope = App.Services.CreateScope();
        var sequenceService = scope.ServiceProvider.GetRequiredService<ISequenceService>();

        var result = await sequenceService.TryEnrollContactBySequenceNameAsync(
            "TestSequencetry-draft",
            new[] { contactId });

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryEnrollByName_ModeNotEnabled_ReturnsFalse()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("try-nomode", delayMinutes: 1440);

        // Restrict enrollment to manual only (remove api)
        await PatchTest($"{SequencesUrl}/{sequenceId}", new
        {
            Enrollment = new SequenceEnrollmentConfig
            {
                Modes = new[] { "manual" },
                ReentryPolicy = ReentryPolicy.OnceEver,
            },
        });

        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("try-nomode");

        using var scope = App.Services.CreateScope();
        var sequenceService = scope.ServiceProvider.GetRequiredService<ISequenceService>();

        var result = await sequenceService.TryEnrollContactBySequenceNameAsync(
            "TestSequencetry-nomode",
            new[] { contactId },
            source: SequenceEnrollmentSource.Api);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryEnrollByName_ReentryPolicyViolation_ReturnsFalse()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("try-reentry", delayMinutes: 1440);
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("try-reentry");

        // First enrollment via API (succeeds normally)
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        // Second enrollment via TryEnroll should return false (OnceEver policy)
        using var scope = App.Services.CreateScope();
        var sequenceService = scope.ServiceProvider.GetRequiredService<ISequenceService>();

        var result = await sequenceService.TryEnrollContactBySequenceNameAsync(
            "TestSequencetry-reentry",
            new[] { contactId });

        result.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────
    // Helper Methods
    // ──────────────────────────────────────────────────

    private static string DeliveriesUrl(int sequenceId) => $"{SequencesUrl}/{sequenceId}/deliveries";

    private static int ExtractId(string location) => int.Parse(location.Split("/").Last());

    private async Task<int> CreateContactAsync(string uid)
    {
        var contact = TestData.Generate<TestContact>(uid);
        var location = await PostTest(ContactsUrl, contact);
        return ExtractId(location);
    }

    private async Task<int> CreateStaticSegmentAsync(string uid, int[] contactIds)
    {
        var segment = new TestSegment(uid, SegmentType.Static, null, contactIds);
        var location = await PostTest(SegmentsUrl, segment);
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

    private async Task UnsubscribeContactAsync(int contactId)
    {
        var unsubscribeDto = new UnsubscribeDto
        {
            ContactId = contactId,
            Reason = "Test unsubscribe",
            Source = "Tests",
        };

        await PostTest("/api/unsubscribes", unsubscribeDto);
    }

    private async Task<(int sequenceId, int templateId)> CreateSequenceWithStepAsync(string uid, int delayMinutes = 0)
    {
        var templateId = await CreateEmailTemplateAsync(uid);
        var sequence = new TestSequence(uid);
        sequence.Steps = new List<SequenceStepCreateDto>
        {
            new() { Name = $"Test Step {uid}", EmailTemplateId = templateId, Timing = new SequenceStepTiming { Delay = new SequenceStepDelay { Value = delayMinutes, Unit = "minutes" } } },
        };
        var created = await PostTest<SequenceDetailsDto>(SequencesUrl, sequence, HttpStatusCode.Created);
        return (created!.Id, templateId);
    }

    private async Task ExecuteSequenceSendTask()
    {
        var response = await GetRequest($"{TasksUrl}/execute/SequenceSendTask");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
