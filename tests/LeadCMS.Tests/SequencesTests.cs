// <copyright file="SequencesTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

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

        var update = new { Name = "UpdatedName", Description = "Updated description", StopOnReply = true };
        await PatchTest($"{SequencesUrl}/{id}", update);

        var updated = await GetTest<SequenceDetailsDto>($"{SequencesUrl}/{id}");
        updated!.Name.Should().Be("UpdatedName");
        updated.Description.Should().Be("Updated description");
        updated.StopOnReply.Should().BeTrue();
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
    // Step Management Tests
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateStep_AutoAssignsPosition()
    {
        var (sequenceId, templateId) = await CreateSequenceWithPrerequisitesAsync("step-pos");

        var step1 = new TestSequenceStep("1", templateId);
        await PostTest<SequenceStepDetailsDto>(StepsUrl(sequenceId), step1, HttpStatusCode.Created);

        var step2 = new TestSequenceStep("2", templateId);
        var result = await PostTest<SequenceStepDetailsDto>(StepsUrl(sequenceId), step2, HttpStatusCode.Created);

        result!.Position.Should().Be(2);
    }

    [Fact]
    public async Task ListSteps_OrderedByPosition()
    {
        var (sequenceId, templateId) = await CreateSequenceWithPrerequisitesAsync("step-list");

        await PostTest<SequenceStepDetailsDto>(StepsUrl(sequenceId), new TestSequenceStep("a", templateId), HttpStatusCode.Created);
        await PostTest<SequenceStepDetailsDto>(StepsUrl(sequenceId), new TestSequenceStep("b", templateId), HttpStatusCode.Created);

        var steps = await GetTest<List<SequenceStepDetailsDto>>(StepsUrl(sequenceId));
        steps.Should().NotBeNull();
        steps!.Count.Should().Be(2);
        steps[0].Position.Should().BeLessThan(steps[1].Position);
    }

    [Fact]
    public async Task DeleteStep_ReordersRemaining()
    {
        var (sequenceId, templateId) = await CreateSequenceWithPrerequisitesAsync("step-del");

        var step1 = await PostTest<SequenceStepDetailsDto>(StepsUrl(sequenceId), new TestSequenceStep("x", templateId), HttpStatusCode.Created);
        var step2 = await PostTest<SequenceStepDetailsDto>(StepsUrl(sequenceId), new TestSequenceStep("y", templateId), HttpStatusCode.Created);
        await PostTest<SequenceStepDetailsDto>(StepsUrl(sequenceId), new TestSequenceStep("z", templateId), HttpStatusCode.Created);

        // Delete the first step
        await DeleteTest($"{StepsUrl(sequenceId)}/{step1!.Id}");

        var steps = await GetTest<List<SequenceStepDetailsDto>>(StepsUrl(sequenceId));
        steps!.Count.Should().Be(2);
        steps[0].Id.Should().Be(step2!.Id);
        steps[0].Position.Should().Be(1);
    }

    [Fact]
    public async Task ReorderSteps_UpdatesPositions()
    {
        var (sequenceId, templateId) = await CreateSequenceWithPrerequisitesAsync("step-reorder");

        var step1 = await PostTest<SequenceStepDetailsDto>(StepsUrl(sequenceId), new TestSequenceStep("r1", templateId), HttpStatusCode.Created);
        var step2 = await PostTest<SequenceStepDetailsDto>(StepsUrl(sequenceId), new TestSequenceStep("r2", templateId), HttpStatusCode.Created);

        // Reverse order
        var result = await PostTest<List<SequenceStepDetailsDto>>(
            $"{StepsUrl(sequenceId)}/reorder",
            new { StepIds = new[] { step2!.Id, step1!.Id } },
            HttpStatusCode.OK);

        result!.Count.Should().Be(2);
        result[0].Id.Should().Be(step2.Id);
        result[0].Position.Should().Be(1);
        result[1].Id.Should().Be(step1.Id);
        result[1].Position.Should().Be(2);
    }

    [Fact]
    public async Task UpdateStep_ChangesTiming()
    {
        var (sequenceId, templateId) = await CreateSequenceWithPrerequisitesAsync("step-update");
        var step = await PostTest<SequenceStepDetailsDto>(StepsUrl(sequenceId), new TestSequenceStep("upd", templateId), HttpStatusCode.Created);

        var newTiming = new SequenceStepTiming
        {
            Delay = new SequenceStepDelay { Value = 2, Unit = "days" },
            SendAt = "10:00",
        };

        await PatchTest($"{StepsUrl(sequenceId)}/{step!.Id}", new { Timing = newTiming });

        var updated = await GetTest<SequenceStepDetailsDto>($"{StepsUrl(sequenceId)}/{step.Id}");
        updated!.Timing.Delay.Value.Should().Be(2);
        updated.Timing.Delay.Unit.Should().Be("days");
        updated.Timing.SendAt.Should().Be("10:00");
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
    public async Task EnrollContact_Always_RejectsWhileActive()
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

        // Re-enrollment while active should fail
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.UnprocessableEntity);
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
        created.Steps[0].Position.Should().Be(1);
        created.Steps[1].Name.Should().Be("follow-up");
        created.Steps[1].Position.Should().Be(2);
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
        updated.Steps.Should().HaveCount(2);
        updated.Steps[0].Name.Should().Be("step-b");
        updated.Steps[0].Position.Should().Be(1);
        updated.Steps[0].Timing.Delay.Value.Should().Be(2);
        updated.Steps[1].Name.Should().Be("step-c");
        updated.Steps[1].Position.Should().Be(2);
    }

    [Fact]
    public async Task Put_WhenActive_Returns422()
    {
        var templateId = await CreateEmailTemplateAsync("put-active");

        var createDto = new SequenceCreateDto
        {
            Name = "ActiveSeq",
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
        var sequenceLocation = await PostTest(SequencesUrl, new TestSequence("multi-step"));
        var sequenceId = ExtractId(sequenceLocation);

        // Add two steps with 0-minute delay
        await PostTest<SequenceStepDetailsDto>(StepsUrl(sequenceId), new TestSequenceStep("ms1", templateId), HttpStatusCode.Created);
        await PostTest<SequenceStepDetailsDto>(StepsUrl(sequenceId), new TestSequenceStep("ms2", templateId), HttpStatusCode.Created);

        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("multi-step");
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        // Execute the task enough times to process all steps
        for (int i = 0; i < 6; i++)
        {
            await ExecuteSequenceSendTask();
        }

        var completedEnrollments = await GetTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments?filter[where][Status]=Completed");
        completedEnrollments!.Count.Should().Be(1);

        var stats = await GetTest<SequenceStatisticsDto>($"{SequencesUrl}/{sequenceId}/statistics");
        stats!.SentCount.Should().Be(2);
    }

    [Fact]
    public async Task SequenceSendTask_DuplicateActiveEnrollments_SchedulesSingleDelivery()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("dup-active-enrollment");
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("dup-active-enrollment");

        using (var scope = App.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();

            await dbContext.SequenceEnrollments!.AddRangeAsync(
                new SequenceEnrollment
                {
                    SequenceId = sequenceId,
                    ContactId = contactId,
                    Status = SequenceEnrollmentStatus.Active,
                    EnteredAt = DateTime.UtcNow.AddMinutes(-10),
                    EnrollmentSource = SequenceEnrollmentSource.Manual,
                    EnrollmentReason = "duplicate-test-1",
                },
                new SequenceEnrollment
                {
                    SequenceId = sequenceId,
                    ContactId = contactId,
                    Status = SequenceEnrollmentStatus.Active,
                    EnteredAt = DateTime.UtcNow.AddMinutes(-5),
                    EnrollmentSource = SequenceEnrollmentSource.Manual,
                    EnrollmentReason = "duplicate-test-2",
                });

            await dbContext.SaveChangesAsync();
        }

        await ExecuteSequenceSendTask();

        using (var scope = App.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();
            var deliveries = await dbContext.SequenceDeliveries!
                .Where(d => d.SequenceId == sequenceId && d.ContactId == contactId)
                .ToListAsync();

            deliveries.Should().HaveCount(1);
            deliveries[0].Status.Should().Be(SequenceDeliveryStatus.Sent);
        }
    }

    // ──────────────────────────────────────────────────
    // Helper Methods
    // ──────────────────────────────────────────────────

    private static string StepsUrl(int sequenceId) => $"{SequencesUrl}/{sequenceId}/steps";

    private static string DeliveriesUrl(int sequenceId) => $"{SequencesUrl}/{sequenceId}/deliveries";

    private static int ExtractId(string location) => int.Parse(location.Split("/").Last());

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

    private async Task<(int sequenceId, int templateId)> CreateSequenceWithPrerequisitesAsync(string uid)
    {
        var templateId = await CreateEmailTemplateAsync(uid);
        var location = await PostTest(SequencesUrl, new TestSequence(uid));
        var sequenceId = ExtractId(location);
        return (sequenceId, templateId);
    }

    private async Task<(int sequenceId, int templateId)> CreateSequenceWithStepAsync(string uid, int delayMinutes = 0)
    {
        var (sequenceId, templateId) = await CreateSequenceWithPrerequisitesAsync(uid);
        await PostTest<SequenceStepDetailsDto>(StepsUrl(sequenceId), new TestSequenceStep(uid, templateId, delayMinutes), HttpStatusCode.Created);
        return (sequenceId, templateId);
    }

    private async Task ExecuteSequenceSendTask()
    {
        var response = await GetRequest($"{TasksUrl}/execute/SequenceSendTask");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
