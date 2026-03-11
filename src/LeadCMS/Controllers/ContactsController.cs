// <copyright file="ContactsController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Net;
using AutoMapper;
using LeadCMS.Attributes;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Exceptions;
using LeadCMS.Helpers;
using LeadCMS.Infrastructure;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace LeadCMS.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class ContactsController : BaseControllerWithImport<Contact, ContactCreateDto, ContactUpdateDto, ContactDetailsDto, ContactImportDto>
{
    private readonly IContactService contactService;
    private readonly IContactEmailCommunicationService contactEmailCommunicationService;
    private readonly CommentableControllerExtension commentableControllerExtension;
    private readonly ISegmentService segmentService;

    public ContactsController(
        PgDbContext dbContext,
        IMapper mapper,
        IContactService contactService,
        IContactEmailCommunicationService contactEmailCommunicationService,
        ISegmentService segmentService,
        EsDbContext esDbContext,
        QueryProviderFactory<Contact> queryProviderFactory,
        CommentableControllerExtension commentableControllerExtension,
        ISyncService syncService)
        : base(dbContext, mapper, esDbContext, queryProviderFactory, syncService)
    {
        this.contactService = contactService;
        this.contactEmailCommunicationService = contactEmailCommunicationService;
        this.commentableControllerExtension = commentableControllerExtension;
        this.segmentService = segmentService;
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<ContactDetailsDto>> GetOne(int id)
    {
        var returnedSingleItem = (await base.GetOne(id)).Result;

        var singleItem = (ContactDetailsDto)((ObjectResult)returnedSingleItem!).Value!;

        singleItem!.AvatarUrl = GravatarHelper.EmailToGravatarUrl(singleItem.Email);
        singleItem.UserDeviceSummary = UserAgentDeviceSummaryHelper.Parse(singleItem.UpdatedByUserAgent ?? singleItem.CreatedByUserAgent);

        return Ok(singleItem!);
    }

    [HttpGet]
    [SegmentIdParameter]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<List<ContactDetailsDto>>> Get([FromQuery] string? query)
    {
        var returnedItems = (await ExecuteWithSegmentFilterAsync(() => base.Get(query))).Result;

        var items = (List<ContactDetailsDto>)((ObjectResult)returnedItems!).Value!;

        items.ForEach(c =>
        {
            c.AvatarUrl = GravatarHelper.EmailToGravatarUrl(c.Email);
        });

        return Ok(items);
    }

    [HttpGet("export")]
    [SegmentIdParameter]
    [Produces("text/csv", "text/json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override Task<ActionResult<List<ContactDetailsDto>>> Export([FromQuery] string? query)
    {
        return ExecuteWithSegmentFilterAsync(() => base.Export(query));
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<ContactDetailsDto>> Post([FromBody] ContactCreateDto value)
    {
        var contact = mapper.Map<Contact>(value);

        await contactService.SaveAsync(contact);

        await dbContext.SaveChangesAsync();

        var returnedValue = mapper.Map<ContactDetailsDto>(contact);

        returnedValue.AvatarUrl = GravatarHelper.EmailToGravatarUrl(returnedValue.Email);

        return CreatedAtAction(nameof(GetOne), new { id = contact.Id }, returnedValue);
    }

    [HttpPatch("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<ContactDetailsDto>> Patch(int id, [FromBody] ContactUpdateDto value)
    {
        var existingContact = (from contact in dbContext.Contacts where contact.Id == id select contact).FirstOrDefault();

        if (existingContact == null)
        {
            throw new EntityNotFoundException("Contact", id.ToString());
        }

        // AutoMapper handles both non-null properties and explicitly-null properties
        mapper.Map(value, existingContact);

        await contactService.SaveAsync(existingContact);

        await dbContext.SaveChangesAsync();

        var returnedValue = mapper.Map<ContactDetailsDto>(existingContact);

        returnedValue.AvatarUrl = GravatarHelper.EmailToGravatarUrl(returnedValue.Email);

        return Ok(returnedValue);
    }

    [HttpGet("{id}/email-communications")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<ContactEmailCommunicationListItemDto>>> GetEmailCommunications(int id)
    {
        await FindOrThrowNotFound(id);

        var queryString = Request.QueryString.HasValue ? Request.QueryString.ToString() : string.Empty;
        var result = await contactEmailCommunicationService.GetCommunicationsAsync(id, queryString);

        var records = result.Records ?? new List<EmailLog>();
        var mappedItems = mapper.Map<List<ContactEmailCommunicationListItemDto>>(records);

        for (var i = 0; i < records.Count; i++)
        {
            mappedItems[i].Body = contactEmailCommunicationService.PrepareBody(records[i]);
        }

        Response.Headers.Append(ResponseHeaderNames.TotalCount, result.TotalCount.ToString());
        Response.Headers.Append(ResponseHeaderNames.AccessControlExposeHeader, ResponseHeaderNames.TotalCount);

        return Ok(mappedItems);
    }

    [HttpGet("{id}/email-communications/{emailLogId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ContactEmailCommunicationDetailsDto>> GetEmailCommunication(int id, int emailLogId)
    {
        await FindOrThrowNotFound(id);

        var emailLog = await contactEmailCommunicationService.GetCommunicationAsync(id, emailLogId);
        var dto = mapper.Map<ContactEmailCommunicationDetailsDto>(emailLog);
        dto.Body = contactEmailCommunicationService.PrepareBody(emailLog);
        return Ok(dto);
    }

    [HttpGet("{id}/email-communications/stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ContactEmailCommunicationStatsDto>> GetEmailCommunicationStats(
        int id,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] EmailCommunicationStatsGroupBy groupBy = EmailCommunicationStatsGroupBy.Day)
    {
        await FindOrThrowNotFound(id);

        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            ModelState.AddModelError(nameof(from), "from should not be greater than to.");
            throw new InvalidModelStateException(ModelState);
        }

        var stats = await contactEmailCommunicationService.GetStatsAsync(id, from, to, groupBy);
        return Ok(stats);
    }

    [HttpGet("{id}/comments")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<CommentDetailsDto>>> GetComments(int id)
    {
        return commentableControllerExtension.ReturnComments(await commentableControllerExtension.GetCommentsForICommentable<Contact>(id), this);
    }

    [HttpPost("{id}/comments")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CommentDetailsDto>> PostComment(int id, [FromBody] CommentCreateBaseDto value)
    {
        return await commentableControllerExtension.PostComment(commentableControllerExtension.CreateCommentForICommentable<Contact>(value, id), this);
    }

    /// <inheritdoc/>
    [HttpGet("sync")]
    [SegmentIdParameter]
    [ProducesResponseType(typeof(SyncResponseDto<ContactDetailsDto, int>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override Task<IActionResult> Sync([FromQuery] string? syncToken = null, [FromQuery] string? query = null)
    {
        return ExecuteWithSegmentFilterAsync(() => base.Sync(syncToken, query));
    }

    protected override async Task SaveRangeAsync(List<Contact> newRecords)
    {
        await contactService.SaveRangeAsync(newRecords);
    }

    private async Task<TResult> ExecuteWithSegmentFilterAsync<TResult>(Func<Task<TResult>> action)
    {
        if (!TryGetSegmentId(out var segmentId))
        {
            return await action();
        }

        var originalQueryString = Request.QueryString;
        var segmentContactIds = await segmentService.GetSegmentContactIdsAsync(segmentId);
        Request.QueryString = BuildSegmentFilteredQueryString(segmentContactIds);

        try
        {
            return await action();
        }
        finally
        {
            Request.QueryString = originalQueryString;
        }
    }

    private bool TryGetSegmentId(out int segmentId)
    {
        segmentId = 0;

        if (!Request.Query.TryGetValue("segmentId", out var segmentIdValues)
            || string.IsNullOrWhiteSpace(segmentIdValues.FirstOrDefault()))
        {
            return false;
        }

        if (int.TryParse(segmentIdValues.First(), out segmentId))
        {
            return true;
        }

        throw new QueryException("segmentId", $"Failed to parse number '{segmentIdValues.First()}'");
    }

    private QueryString BuildSegmentFilteredQueryString(IEnumerable<int> segmentContactIds)
    {
        var normalizedParameters = GetNormalizedQueryParameters();
        var segmentIds = segmentContactIds.Distinct().ToHashSet();
        var existingIds = ParseFilterIds(normalizedParameters);
        var effectiveIds = existingIds.Count > 0
            ? segmentIds.Intersect(existingIds).ToArray()
            : segmentIds.ToArray();

        var queryBuilder = new QueryBuilder();
        foreach (var parameter in normalizedParameters)
        {
            if (string.Equals(parameter.Key, "segmentId", StringComparison.OrdinalIgnoreCase)
                || string.Equals(parameter.Key, "filter[ids]", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            queryBuilder.Add(parameter.Key, parameter.Value);
        }

        queryBuilder.Add("filter[ids]", effectiveIds.Length > 0 ? string.Join(',', effectiveIds) : "-1");

        return queryBuilder.ToQueryString();
    }

    private List<KeyValuePair<string, string>> GetNormalizedQueryParameters()
    {
        var normalizedParameters = new List<KeyValuePair<string, string>>();

        foreach (var parameter in Request.Query)
        {
            foreach (var value in parameter.Value)
            {
                if (string.Equals(parameter.Key, "query", StringComparison.OrdinalIgnoreCase)
                    && TryExpandEmbeddedQueryParameters(value, out var embeddedParameters))
                {
                    normalizedParameters.AddRange(embeddedParameters);
                    continue;
                }

                normalizedParameters.Add(new KeyValuePair<string, string>(parameter.Key, value ?? string.Empty));
            }
        }

        return normalizedParameters;
    }

    private HashSet<int> ParseFilterIds(IEnumerable<KeyValuePair<string, string>> parameters)
    {
        return parameters
            .Where(parameter => string.Equals(parameter.Key, "filter[ids]", StringComparison.OrdinalIgnoreCase))
            .Select(parameter => parameter.Value)
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(value => int.TryParse(value, out var parsedValue) ? (int?)parsedValue : null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToHashSet();
    }

    private bool TryExpandEmbeddedQueryParameters(string? queryValue, out List<KeyValuePair<string, string>> parameters)
    {
        parameters = new List<KeyValuePair<string, string>>();

        if (string.IsNullOrWhiteSpace(queryValue))
        {
            return false;
        }

        var normalizedQuery = queryValue.Trim().TrimStart('?', '&');
        if (!normalizedQuery.Contains("filter[", StringComparison.OrdinalIgnoreCase) || !normalizedQuery.Contains('='))
        {
            return false;
        }

        foreach (var pair in normalizedQuery.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
            {
                continue;
            }

            parameters.Add(new KeyValuePair<string, string>(
                WebUtility.UrlDecode(parts[0]),
                WebUtility.UrlDecode(parts[1])));
        }

        return parameters.Count > 0;
    }
}
