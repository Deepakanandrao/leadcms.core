// <copyright file="TaskStatusService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Collections.Concurrent;

namespace LeadCMS.Services;

public class TaskStatusService
{
    private readonly ConcurrentDictionary<string, bool> taskStatusByName = new ConcurrentDictionary<string, bool>();

    public void SetInitialState(string name, bool running)
    {
        taskStatusByName.TryAdd(name, running);
    }

    public bool IsRunning(string name)
    {
        return taskStatusByName.TryGetValue(name, out var running) && running;
    }

    public void SetRunning(string name, bool running)
    {
        taskStatusByName[name] = running;
    }
}