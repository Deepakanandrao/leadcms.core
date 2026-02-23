// <copyright file="CampaignsController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Infrastructure;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class CampaignsController : BaseController<Campaign, CampaignCreateDto, CampaignUpdateDto, CampaignDetailsDto>
{
    private readonly ICampaignService campaignService;
    private readonly QueryProviderFactory<CampaignRecipient> recipientQueryProviderFactory;

    public CampaignsController(
        PgDbContext dbContext,
        IMapper mapper,
        EsDbContext esDbContext,
        QueryProviderFactory<Campaign> queryProviderFactory,
        QueryProviderFactory<CampaignRecipient> recipientQueryProviderFactory,
        ICampaignService campaignService,
        ISyncService syncService)
        : base(dbContext, mapper, esDbContext, queryProviderFactory, syncService)
    {
        this.campaignService = campaignService;
        this.recipientQueryProviderFactory = recipientQueryProviderFactory;
    }

    /// <summary>
    /// Creates a new campaign. Status is always Draft on creation.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<CampaignDetailsDto>> Post([FromBody] CampaignCreateDto value)
    {
        var campaign = mapper.Map<Campaign>(value);
        campaign.Status = CampaignStatus.Draft;

        if (string.IsNullOrWhiteSpace(campaign.Language))
        {
            campaign.Language = LanguageHelper.GetDefaultLanguage(dbContext.Configuration);
        }

        // Validate template exists
        var template = await dbContext.EmailTemplates!.FindAsync(campaign.EmailTemplateId);
        if (template == null)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Invalid email template",
                Detail = $"Email template with id {campaign.EmailTemplateId} not found.",
            });
        }

        // Validate segments exist
        foreach (var segmentId in campaign.SegmentIds)
        {
            var segment = await dbContext.Segments!.FindAsync(segmentId);
            if (segment == null)
            {
                return UnprocessableEntity(new ProblemDetails
                {
                    Title = "Invalid segment",
                    Detail = $"Segment with id {segmentId} not found.",
                });
            }
        }

        await dbSet.AddAsync(campaign);
        await dbContext.SaveChangesAsync();

        var dto = mapper.Map<CampaignDetailsDto>(campaign);
        return CreatedAtAction(nameof(GetOne), new { id = campaign.Id }, dto);
    }

    /// <summary>
    /// Updates a campaign.
    /// Draft campaigns allow all fields.
    /// Scheduled campaigns allow scheduling fields only (ScheduledAt, TimeZone, UseContactTimeZone).
    /// </summary>
    [HttpPatch("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<CampaignDetailsDto>> Patch(int id, [FromBody] CampaignUpdateDto value)
    {
        var existingEntity = await FindOrThrowNotFound(id);

        if (existingEntity.Status == CampaignStatus.Draft)
        {
            return await Patch(existingEntity, value);
        }

        if (existingEntity.Status == CampaignStatus.Scheduled)
        {
            if (HasNonSchedulingChanges(value))
            {
                return UnprocessableEntity(new ProblemDetails
                {
                    Title = "Cannot edit campaign",
                    Detail = "This campaign is in scheduled status. You can update scheduling fields only. Audience and template/content are locked to prevent recipient/content drift.",
                });
            }

            return await Patch(existingEntity, value);
        }

        return UnprocessableEntity(new ProblemDetails
        {
            Title = "Cannot edit campaign",
            Detail = $"Campaign can only be edited in Draft status. Current status: {existingEntity.Status}.",
        });
    }

    /// <summary>
    /// Launches a campaign: transitions it from Draft to Sending or Scheduled.
    /// </summary>
    [HttpPost("{id}/launch")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CampaignDetailsDto>> Launch(int id, [FromBody] CampaignLaunchDto launchDto)
    {
        var campaign = await campaignService.LaunchAsync(id, launchDto);
        var dto = mapper.Map<CampaignDetailsDto>(campaign);
        return Ok(dto);
    }

    /// <summary>
    /// Cancels a scheduled campaign.
    /// </summary>
    [HttpPost("{id}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CampaignDetailsDto>> Cancel(int id)
    {
        var campaign = await campaignService.CancelAsync(id);
        var dto = mapper.Map<CampaignDetailsDto>(campaign);
        return Ok(dto);
    }

    /// <summary>
    /// Pauses a sending campaign.
    /// </summary>
    [HttpPost("{id}/pause")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CampaignDetailsDto>> Pause(int id)
    {
        var campaign = await campaignService.PauseAsync(id);
        var dto = mapper.Map<CampaignDetailsDto>(campaign);
        return Ok(dto);
    }

    /// <summary>
    /// Resumes a paused campaign.
    /// </summary>
    [HttpPost("{id}/resume")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CampaignDetailsDto>> Resume(int id)
    {
        var campaign = await campaignService.ResumeAsync(id);
        var dto = mapper.Map<CampaignDetailsDto>(campaign);
        return Ok(dto);
    }

    /// <summary>
    /// Gets campaign statistics with skip reason breakdown.
    /// </summary>
    [HttpGet("{id}/statistics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampaignStatisticsDto>> GetStatistics(int id)
    {
        var stats = await campaignService.GetStatisticsAsync(id);
        return Ok(stats);
    }

    /// <summary>
    /// Generates a campaign preview including audience statistics and a rendered email template preview.
    /// Does not require a saved campaign — useful for previewing before creating one.
    /// </summary>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(CampaignPreviewResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CampaignPreviewResultDto>> Preview([FromBody] CampaignPreviewRequestDto dto)
    {
        var result = await campaignService.PreviewAsync(dto);
        return Ok(result);
    }

    /// <summary>
    /// Gets recipients for a campaign with full query support (filtering, sorting, paging).
    /// </summary>
    [HttpGet("{id}/recipients")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<CampaignRecipientDetailsDto>>> GetRecipients(int id, [FromQuery] string? query)
    {
        var campaign = await dbContext.Campaigns!.FindAsync(id);
        if (campaign == null)
        {
            return NotFound(new ProblemDetails { Title = "Not found", Detail = $"Campaign with id {id} not found." });
        }

        var qp = recipientQueryProviderFactory.BuildQueryProvider(
            additionalQueryString: $"filter[where][campaignId]={id}&filter[include]=Contact&filter[include]=Campaign");

        var result = await qp.GetResult();
        Response.Headers.Append(ResponseHeaderNames.TotalCount, result.TotalCount.ToString());
        Response.Headers.Append(ResponseHeaderNames.AccessControlExposeHeader, ResponseHeaderNames.TotalCount);

        var recipients = result.Records?.ToList() ?? new List<CampaignRecipient>();
        var dtos = mapper.Map<List<CampaignRecipientDetailsDto>>(recipients);

        foreach (var dto in dtos)
        {
            var recipient = recipients.FirstOrDefault(r => r.Id == dto.Id);
            if (recipient != null)
            {
                dto.ExpectedSendAtUtc = CampaignScheduleHelper.GetExpectedSendAtUtc(recipient);
            }
        }

        return Ok(dtos);
    }

    private static bool HasNonSchedulingChanges(CampaignUpdateDto value)
    {
        return IsProvided(value.Name, nameof(CampaignUpdateDto.Name), value)
            || IsProvided(value.Description, nameof(CampaignUpdateDto.Description), value)
            || IsProvided(value.EmailTemplateId, nameof(CampaignUpdateDto.EmailTemplateId), value)
            || IsProvided(value.SegmentIds, nameof(CampaignUpdateDto.SegmentIds), value)
            || IsProvided(value.ExcludeSegmentIds, nameof(CampaignUpdateDto.ExcludeSegmentIds), value)
            || IsProvided(value.Language, nameof(CampaignUpdateDto.Language), value);
    }

    private static bool IsProvided<T>(T? propertyValue, string propertyName, CampaignUpdateDto dto)
    {
        return propertyValue != null || dto.NullProperties.Contains(propertyName);
    }
}
