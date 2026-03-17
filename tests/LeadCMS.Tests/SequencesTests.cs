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
        var (sequenceId, _) = await CreateSequenceWithStepAsync("archive-enroll");
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
        var (sequenceId, _) = await CreateSequenceWithStepAsync("enroll");
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
    public async Task RemoveEnrollment_SetsExitedStatus()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("remove-enroll");
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
    public async Task ListEnrollments_FiltersByStatus()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("list-enroll");
        await PostTest<SequenceDetailsDto>($"{SequencesUrl}/{sequenceId}/activate", new { }, HttpStatusCode.OK);

        var contactId = await CreateContactAsync("list-enroll");
        await PostTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments",
            new SequenceEnrollmentCreateDto { ContactIds = new[] { contactId } },
            HttpStatusCode.Created);

        var activeEnrollments = await GetTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments?status=Active");
        activeEnrollments.Should().NotBeNull();
        activeEnrollments!.Count.Should().Be(1);

        var completedEnrollments = await GetTest<List<SequenceEnrollmentDetailsDto>>(
            $"{SequencesUrl}/{sequenceId}/enrollments?status=Completed");
        completedEnrollments!.Count.Should().Be(0);
    }

    // ──────────────────────────────────────────────────
    // Statistics Tests
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task GetStatistics_ReturnsCorrectCounts()
    {
        var (sequenceId, _) = await CreateSequenceWithStepAsync("stats");
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
            $"{SequencesUrl}/{sequenceId}/enrollments?status=Completed");
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
            $"{SequencesUrl}/{sequenceId}/enrollments?status=Completed");
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

    private async Task<(int sequenceId, int templateId)> CreateSequenceWithStepAsync(string uid)
    {
        var (sequenceId, templateId) = await CreateSequenceWithPrerequisitesAsync(uid);
        await PostTest<SequenceStepDetailsDto>(StepsUrl(sequenceId), new TestSequenceStep(uid, templateId), HttpStatusCode.Created);
        return (sequenceId, templateId);
    }

    private async Task ExecuteSequenceSendTask()
    {
        var response = await GetRequest($"{TasksUrl}/execute/SequenceSendTask");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
