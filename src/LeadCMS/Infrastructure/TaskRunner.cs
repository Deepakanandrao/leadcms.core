// <copyright file="TaskRunner.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Configuration;
using LeadCMS.Data;
using LeadCMS.Entities;
using LeadCMS.Interfaces;
using Medallion.Threading.Postgres;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace LeadCMS.Infrastructure
{
    public class TaskRunner : IJob
    {
        private const string TaskRunnerNodeLockKey = "TaskRunnerPrimaryNodeLock";

        private static readonly object PrimaryNodeStatusLock = new();
        private static (PostgresDistributedLockHandle?, PrimaryNodeStatus) primaryNodeStatus = (null, PrimaryNodeStatus.Unknown);

        private readonly IEnumerable<ITask> tasks;
        private readonly PgDbContext dbContext;

        public TaskRunner(IEnumerable<ITask> tasks, PgDbContext dbContext)
        {
            this.dbContext = dbContext;
            this.tasks = tasks;

            lock (PrimaryNodeStatusLock)
            {
                if (primaryNodeStatus.Item2 == PrimaryNodeStatus.Unknown)
                {
#pragma warning disable S3010
                    primaryNodeStatus = GetPrimaryStatus();
                }
            }

            Log.Information("This node: " + (IsPrimaryNode() ? "is primary" : "isn't primary"));
        }

        private enum PrimaryNodeStatus
        {
            Primary,
            NonPrimary,
            Unknown,
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                if (!IsPrimaryNode())
                {
                    Log.Information("This is not the current primary node for task execution");
                    return;
                }

                foreach (var task in tasks.Where(t => t.IsRunning))
                {
                    var postgresConfig = dbContext.Configuration.GetSection("Postgres").Get<PostgresConfig>()!;
                    var taskLock = LockManager.GetNoWaitLock(task.Name, postgresConfig.ConnectionString).Item1;

                    if (taskLock is null)
                    {
                        Log.Information($"Skipping the task {task.Name} as the previous run is not completed yet.");
                        continue;
                    }

                    using (taskLock)
                    {
                        var currentJob = await AddOrGetPendingTaskExecutionLog(task, TaskExecutionTrigger.Scheduled);

                        if (IsRightTimeToExecute(currentJob, task))
                        {
                            currentJob.ActualExecutionTime = DateTime.UtcNow;
                            var isCompleted = await task.Execute(currentJob);

                            await UpdateTaskExecutionLog(currentJob, isCompleted ? TaskExecutionStatus.Completed : TaskExecutionStatus.Pending);

                            if (isCompleted)
                            {
                                await CleanupOldLogsAsync(task);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error executing task runner");
            }
        }

        public async Task<bool> ExecuteTask(ITask task, TaskExecutionTrigger trigger = TaskExecutionTrigger.Manual)
        {
            if (!IsPrimaryNode())
            {
                throw new NonPrimaryNodeException();
            }

            var postgresConfig = dbContext.Configuration.GetSection("Postgres").Get<PostgresConfig>()!;
            var taskLock = LockManager.GetNoWaitLock(task.Name, postgresConfig.ConnectionString).Item1;

            if (taskLock is null)
            {
                throw new TaskNotCompletedException();
            }

            using (taskLock)
            {
                var currentJob = await AddOrGetPendingTaskExecutionLog(task, trigger);
                currentJob.ActualExecutionTime = DateTime.UtcNow;

                var isCompleted = await task.Execute(currentJob);

                await UpdateTaskExecutionLog(currentJob, isCompleted ? TaskExecutionStatus.Completed : TaskExecutionStatus.Pending);

                if (isCompleted)
                {
                    await CleanupOldLogsAsync(task);
                }

                return isCompleted;
            }
        }

        public void StartOrStopTask(ITask task, bool start)
        {
            if (!IsPrimaryNode())
            {
                throw new NonPrimaryNodeException();
            }

            task.SetRunning(start);
        }

        private static bool IsPrimaryNode()
        {
            return primaryNodeStatus.Item2 == PrimaryNodeStatus.Primary;
        }

        private (PostgresDistributedLockHandle?, PrimaryNodeStatus) GetPrimaryStatus()
        {
            var postgresConfig = dbContext.Configuration.GetSection("Postgres").Get<PostgresConfig>()!;
            var primaryNodeLockData = LockManager.GetNoWaitLock(TaskRunnerNodeLockKey, postgresConfig.ConnectionString);
            if (primaryNodeLockData.Item2)
            {
                return (primaryNodeLockData.Item1, (primaryNodeLockData.Item1 != null) ? PrimaryNodeStatus.Primary : PrimaryNodeStatus.NonPrimary);
            }
            else
            {
                return (null, PrimaryNodeStatus.Unknown);
            }
        }

        private async Task<TaskExecutionLog> AddOrGetPendingTaskExecutionLog(ITask task, TaskExecutionTrigger trigger = TaskExecutionTrigger.Scheduled)
        {
            var pendingTask = await dbContext.TaskExecutionLogs!.
                FirstOrDefaultAsync(taskLog => taskLog.Status == TaskExecutionStatus.Pending && taskLog.TaskName == task.Name);

            if (pendingTask is not null)
            {
                return pendingTask;
            }

            var now = DateTime.UtcNow;
            pendingTask = new TaskExecutionLog()
            {
                TaskName = task.Name,
                ScheduledExecutionTime = GetExecutionTimeByCronSchedule(task.CronSchedule, now),
                ActualExecutionTime = now,
                Status = TaskExecutionStatus.Pending,
                RetryCount = 0,
                TriggeredBy = trigger,
            };

            await dbContext.TaskExecutionLogs!.AddAsync(pendingTask);
            await dbContext.SaveChangesAsync();

            return pendingTask;
        }

        private async Task UpdateTaskExecutionLog(TaskExecutionLog job, TaskExecutionStatus status)
        {
            var endTime = DateTime.UtcNow;
            job.Status = status;
            job.Duration = endTime - job.ActualExecutionTime;

            if (status == TaskExecutionStatus.Pending)
            {
                job.RetryCount = ++job.RetryCount;
            }

            dbContext!.TaskExecutionLogs!.Update(job);
            await dbContext.SaveChangesAsync();
        }

        private async Task CleanupOldLogsAsync(ITask task)
        {
            var maxRecords = task.MaxLogRecords;

            // Clean up task_execution_log: keep only the last N completed records
            var executionLogCount = await dbContext.TaskExecutionLogs!
                .CountAsync(l => l.TaskName == task.Name && l.Status != TaskExecutionStatus.Pending);

            int removedExecutionLogs = 0;
            if (executionLogCount > maxRecords)
            {
                var excessLogs = await dbContext.TaskExecutionLogs!
                    .Where(l => l.TaskName == task.Name && l.Status != TaskExecutionStatus.Pending)
                    .OrderBy(l => l.ActualExecutionTime)
                    .Take(executionLogCount - maxRecords)
                    .ToListAsync();

                dbContext.TaskExecutionLogs!.RemoveRange(excessLogs);
                removedExecutionLogs = excessLogs.Count;
            }

            // Clean up change_log_task_log: keep only the last N non-in-progress records
            var changeLogCount = await dbContext.ChangeLogTaskLogs!
                .CountAsync(l => l.TaskName == task.Name && l.State != TaskExecutionState.InProgress);

            int removedChangeLogLogs = 0;
            if (changeLogCount > maxRecords)
            {
                var excessLogs = await dbContext.ChangeLogTaskLogs!
                    .Where(l => l.TaskName == task.Name && l.State != TaskExecutionState.InProgress)
                    .OrderBy(l => l.Start)
                    .Take(changeLogCount - maxRecords)
                    .ToListAsync();

                dbContext.ChangeLogTaskLogs!.RemoveRange(excessLogs);
                removedChangeLogLogs = excessLogs.Count;
            }

            if (removedExecutionLogs > 0 || removedChangeLogLogs > 0)
            {
                await dbContext.SaveChangesAsync();
                Log.Information($"Log cleanup for {task.Name}: removed {removedExecutionLogs} execution logs and {removedChangeLogLogs} change log task logs (keeping last {maxRecords}).");
            }
        }

        private bool IsRightTimeToExecute(TaskExecutionLog job, ITask task)
        {
            if (job.RetryCount >= task.RetryCount)
            {
                task.SetRunning(false); // no need to execute task when failed more than RetryCount times
                return false;
            }

            if (job.RetryCount > 0)
            {
                return job.ActualExecutionTime.AddMinutes(task.RetryInterval) <= DateTime.UtcNow;
            }

            return job.ScheduledExecutionTime <= DateTime.UtcNow;
        }

        private DateTime GetExecutionTimeByCronSchedule(string cronSchedule, DateTime baseExecutionTime)
        {
            var expression = new CronExpression(cronSchedule);

            var nextRunTime = expression.GetNextValidTimeAfter(baseExecutionTime);

            return nextRunTime!.Value.UtcDateTime;
        }
    }
}