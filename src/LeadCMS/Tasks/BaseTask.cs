// <copyright file="BaseTask.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Configuration;
using LeadCMS.Entities;
using LeadCMS.Interfaces;
using LeadCMS.Services;

namespace LeadCMS.Tasks;

public abstract class BaseTask : ITask
{
    protected readonly string configKey;

    private readonly TaskStatusService taskStatusService;

    protected BaseTask(string configKey, IConfiguration configuration, TaskStatusService taskStatusService)
    {
        this.configKey = configKey;
        this.taskStatusService = taskStatusService;

        var config = configuration.GetSection(configKey)!.Get<TaskConfig>();

        if (config is not null)
        {
            CronSchedule = config.CronSchedule;
            RetryCount = config.RetryCount;
            RetryInterval = config.RetryInterval;
            MaxLogRecords = config.MaxLogRecords;

            taskStatusService.SetInitialState(Name, config.Enable);
        }
        else
        {
            throw new MissingConfigurationException($"The specified configuration section for the provided configKey {configKey} could not be found in the settings file.");
        }
    }

    public string Name
    {
        get
        {
            return GetType().Name;
        }
    }

    public string CronSchedule { get; private set; }

    public int RetryCount { get; private set; }

    public int RetryInterval { get; private set; }

    public int MaxLogRecords { get; private set; } = 1000;

    public bool IsRunning
    {
        get
        {
            return taskStatusService.IsRunning(Name);
        }
    }

    public void SetRunning(bool running)
    {
        taskStatusService.SetRunning(Name, running);
    }

    public abstract Task<bool> Execute(TaskExecutionLog currentJob);
}