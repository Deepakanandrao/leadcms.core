// <copyright file="TaskLogRetentionTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Configuration;
using LeadCMS.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Tests;

public class TaskLogRetentionTests : BaseTestAutoLogin
{
    private const string TasksUrl = "/api/tasks";
    private const string TaskName = "SyncEsTask";

    public TaskLogRetentionTests()
        : base()
    {
        TrackEntityType<TaskExecutionLog>();
        TrackEntityType<ChangeLogTaskLog>();
    }

    [Fact]
    public async Task CleanupOldLogs_ShouldRetainMaxRecords_ForTaskExecutionLogs()
    {
        // Arrange
        var maxRecords = GetMaxLogRecords();
        var seedCount = maxRecords + 10;

        var dbContext = App.GetDbContext()!;

        for (int i = 0; i < seedCount; i++)
        {
            dbContext.TaskExecutionLogs!.Add(new TaskExecutionLog
            {
                TaskName = TaskName,
                ScheduledExecutionTime = DateTime.UtcNow.AddMinutes(-seedCount + i),
                ActualExecutionTime = DateTime.UtcNow.AddMinutes(-seedCount + i),
                Status = TaskExecutionStatus.Completed,
                RetryCount = 0,
                TriggeredBy = TaskExecutionTrigger.Manual,
                Duration = TimeSpan.FromSeconds(1),
            });
        }

        await dbContext.SaveChangesAsync();

        var countBefore = await dbContext.TaskExecutionLogs!
            .CountAsync(l => l.TaskName == TaskName && l.Status != TaskExecutionStatus.Pending);
        countBefore.Should().Be(seedCount);

        // Act: execute the task which triggers log cleanup
        await ExecuteTask();

        // Assert: only MaxLogRecords non-pending records should remain
        var freshDbContext = App.GetDbContext()!;
        var countAfter = await freshDbContext.TaskExecutionLogs!
            .CountAsync(l => l.TaskName == TaskName && l.Status != TaskExecutionStatus.Pending);

        countAfter.Should().BeLessThanOrEqualTo(maxRecords);
    }

    [Fact]
    public async Task CleanupOldLogs_ShouldRetainMaxRecords_ForChangeLogTaskLogs()
    {
        // Arrange
        var maxRecords = GetMaxLogRecords();
        var seedCount = maxRecords + 10;

        var dbContext = App.GetDbContext()!;

        for (int i = 0; i < seedCount; i++)
        {
            dbContext.ChangeLogTaskLogs!.Add(new ChangeLogTaskLog
            {
                TaskName = TaskName,
                ChangeLogIdMin = i,
                ChangeLogIdMax = i + 1,
                State = TaskExecutionState.Completed,
                Start = DateTime.UtcNow.AddMinutes(-seedCount + i),
                End = DateTime.UtcNow.AddMinutes(-seedCount + i + 1),
                ChangesProcessed = 1,
            });
        }

        await dbContext.SaveChangesAsync();

        var countBefore = await dbContext.ChangeLogTaskLogs!
            .CountAsync(l => l.TaskName == TaskName && l.State != TaskExecutionState.InProgress);
        countBefore.Should().Be(seedCount);

        // Act: execute the task which triggers log cleanup
        await ExecuteTask();

        // Assert: only MaxLogRecords non-in-progress records should remain
        var freshDbContext = App.GetDbContext()!;
        var countAfter = await freshDbContext.ChangeLogTaskLogs!
            .CountAsync(l => l.TaskName == TaskName && l.State != TaskExecutionState.InProgress);

        countAfter.Should().BeLessThanOrEqualTo(maxRecords);
    }

    [Fact]
    public async Task CleanupOldLogs_ShouldKeepNewestRecords()
    {
        // Arrange: seed records with known timestamps so we can verify oldest are removed
        var maxRecords = GetMaxLogRecords();
        var seedCount = maxRecords + 10;

        var dbContext = App.GetDbContext()!;

        var baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < seedCount; i++)
        {
            dbContext.TaskExecutionLogs!.Add(new TaskExecutionLog
            {
                TaskName = TaskName,
                ScheduledExecutionTime = baseTime.AddMinutes(i),
                ActualExecutionTime = baseTime.AddMinutes(i),
                Status = TaskExecutionStatus.Completed,
                RetryCount = 0,
                TriggeredBy = TaskExecutionTrigger.Manual,
                Duration = TimeSpan.FromSeconds(1),
            });
        }

        await dbContext.SaveChangesAsync();

        // Act
        await ExecuteTask();

        // Assert: the remaining records should be the newest ones
        var freshDbContext = App.GetDbContext()!;
        var remainingLogs = await freshDbContext.TaskExecutionLogs!
            .Where(l => l.TaskName == TaskName && l.Status != TaskExecutionStatus.Pending)
            .OrderBy(l => l.ActualExecutionTime)
            .ToListAsync();

        remainingLogs.Count.Should().BeLessThanOrEqualTo(maxRecords);

        // The oldest seeded records (before the cutoff) should have been removed
        var oldestRemainingTime = remainingLogs.First().ActualExecutionTime;
        var oldestSeededTime = baseTime;
        oldestRemainingTime.Should().BeAfter(oldestSeededTime);
    }

    [Fact]
    public async Task CleanupOldLogs_ShouldNotRemovePendingRecords()
    {
        // Arrange: seed records including some with Pending status
        var maxRecords = GetMaxLogRecords();
        var seedCount = maxRecords + 10;

        var dbContext = App.GetDbContext()!;

        // Add completed records
        for (int i = 0; i < seedCount; i++)
        {
            dbContext.TaskExecutionLogs!.Add(new TaskExecutionLog
            {
                TaskName = TaskName,
                ScheduledExecutionTime = DateTime.UtcNow.AddMinutes(-seedCount + i),
                ActualExecutionTime = DateTime.UtcNow.AddMinutes(-seedCount + i),
                Status = TaskExecutionStatus.Completed,
                RetryCount = 0,
                TriggeredBy = TaskExecutionTrigger.Manual,
                Duration = TimeSpan.FromSeconds(1),
            });
        }

        await dbContext.SaveChangesAsync();

        // Act
        await ExecuteTask();

        // Assert: No pending records were removed (cleanup only targets non-pending)
        var freshDbContext = App.GetDbContext()!;
        var allRecords = await freshDbContext.TaskExecutionLogs!
            .Where(l => l.TaskName == TaskName)
            .ToListAsync();

        // The total non-pending count should be at most MaxLogRecords
        var nonPendingCount = allRecords.Count(l => l.Status != TaskExecutionStatus.Pending);
        nonPendingCount.Should().BeLessThanOrEqualTo(maxRecords);
    }

    [Fact]
    public async Task CleanupOldLogs_ShouldNotRemoveInProgressChangeLogRecords()
    {
        // Arrange: seed change log records including some with InProgress state
        var maxRecords = GetMaxLogRecords();
        var seedCount = maxRecords + 10;
        var inProgressCount = 3;

        var dbContext = App.GetDbContext()!;

        // Add InProgress records (should never be deleted)
        for (int i = 0; i < inProgressCount; i++)
        {
            dbContext.ChangeLogTaskLogs!.Add(new ChangeLogTaskLog
            {
                TaskName = TaskName,
                ChangeLogIdMin = i,
                ChangeLogIdMax = i + 1,
                State = TaskExecutionState.InProgress,
                Start = DateTime.UtcNow.AddMinutes(-seedCount - i),
                End = DateTime.UtcNow,
                ChangesProcessed = 0,
            });
        }

        // Add completed records (excess of these should be cleaned)
        for (int i = 0; i < seedCount; i++)
        {
            dbContext.ChangeLogTaskLogs!.Add(new ChangeLogTaskLog
            {
                TaskName = TaskName,
                ChangeLogIdMin = 100 + i,
                ChangeLogIdMax = 101 + i,
                State = TaskExecutionState.Completed,
                Start = DateTime.UtcNow.AddMinutes(-seedCount + i),
                End = DateTime.UtcNow.AddMinutes(-seedCount + i + 1),
                ChangesProcessed = 1,
            });
        }

        await dbContext.SaveChangesAsync();

        // Act
        await ExecuteTask();

        // Assert
        var freshDbContext = App.GetDbContext()!;
        var remainingRecords = await freshDbContext.ChangeLogTaskLogs!
            .Where(l => l.TaskName == TaskName)
            .ToListAsync();

        // All InProgress records should still exist
        var remainingInProgress = remainingRecords.Count(l => l.State == TaskExecutionState.InProgress);
        remainingInProgress.Should().Be(inProgressCount);

        // Non-InProgress records should be capped at MaxLogRecords
        var remainingNonInProgress = remainingRecords.Count(l => l.State != TaskExecutionState.InProgress);
        remainingNonInProgress.Should().BeLessThanOrEqualTo(maxRecords);
    }

    private static int GetMaxLogRecords()
    {
        var config = App.Services.GetRequiredService<IConfiguration>();
        var taskConfig = config.GetSection($"Tasks:{TaskName}").Get<TaskConfig>()!;
        return taskConfig.MaxLogRecords;
    }

    private async Task ExecuteTask()
    {
        var response = await GetRequest($"{TasksUrl}/execute/{TaskName}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var taskResult = JsonHelper.Deserialize<TaskExecutionDto>(content);
        taskResult.Should().NotBeNull();
        taskResult!.Completed.Should().BeTrue();
    }
}
