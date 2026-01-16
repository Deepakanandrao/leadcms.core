// <copyright file="TasksController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Infrastructure;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeadCMS.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly IEnumerable<ITask> tasks;
    private readonly TaskRunner taskRunner;
    private readonly IMapper mapper;
    private readonly QueryProviderFactory<TaskExecutionLog> queryProviderFactory;

    public TasksController(
        IEnumerable<ITask> tasks,
        TaskRunner taskRunner,
        IMapper mapper,
        QueryProviderFactory<TaskExecutionLog> queryProviderFactory)
    {
        this.taskRunner = taskRunner;
        this.tasks = tasks;
        this.mapper = mapper;
        this.queryProviderFactory = queryProviderFactory;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<TaskDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public ActionResult<List<TaskDetailsDto>> Get()
    {
        return Ok(tasks.Select(t => CreateTaskDetailsDto(t)).ToList());
    }

    [HttpGet("{name}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public TaskDetailsDto Get(string name)
    {
        var result = tasks.Where(t => t.Name == name);

        if (!result.Any())
        {
            throw new TaskNotFoundException(name);
        }
        else
        {
            return CreateTaskDetailsDto(result.First());
        }
    }

    [HttpGet("start/{name}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public TaskDetailsDto Start(string name)
    {
        return StartOrStop(name, true);
    }

    [HttpGet("stop/{name}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public TaskDetailsDto Stop(string name)
    {
        return StartOrStop(name, false);
    }

    [HttpGet("execute/{name}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<TaskExecutionDto> Execute(string name)
    {
        var result = tasks.Where(t => t.Name == name);

        if (!result.Any())
        {
            throw new TaskNotFoundException(name);
        }
        else
        {
            var completed = await taskRunner.ExecuteTask(result.First());
            return new TaskExecutionDto
            {
                Name = name,
                Completed = completed,
            };
        }
    }

    [HttpGet("logs")]
    [ProducesResponseType(typeof(List<TaskExecutionLogDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<TaskExecutionLogDetailsDto>>> GetLogs([FromQuery] string? query)
    {
        // Apply default sort by Id DESC if no order is provided
        var queryString = Request.QueryString.HasValue ? Request.QueryString.ToString() : string.Empty;
        if (!queryString.Contains("filter[order]", StringComparison.OrdinalIgnoreCase))
        {
            var separator = string.IsNullOrEmpty(queryString) ? "?" : "&";
            queryString += $"{separator}filter[order]=Id DESC";
            Request.QueryString = new QueryString(queryString);
        }

        var qp = queryProviderFactory.BuildQueryProvider();
        var result = await qp.GetResult();

        Response.Headers.Append(ResponseHeaderNames.TotalCount, result.TotalCount.ToString());
        Response.Headers.Append(ResponseHeaderNames.AccessControlExposeHeader, ResponseHeaderNames.TotalCount);

        return Ok(mapper.Map<List<TaskExecutionLogDetailsDto>>(result.Records));
    }

    private TaskDetailsDto StartOrStop(string name, bool start)
    {
        var result = tasks.Where(t => t.Name == name);

        if (!result.Any())
        {
            throw new TaskNotFoundException(name);
        }
        else
        {
            var task = result.First();
            taskRunner.StartOrStopTask(task, start);
            return CreateTaskDetailsDto(task);
        }
    }

    private TaskDetailsDto CreateTaskDetailsDto(ITask task)
    {
        return new TaskDetailsDto
        {
            Name = task.Name,
            CronSchedule = task.CronSchedule,
            RetryCount = task.RetryCount,
            RetryInterval = task.RetryInterval,
            IsRunning = task.IsRunning,
        };
    }
}