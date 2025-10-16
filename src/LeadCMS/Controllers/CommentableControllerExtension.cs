// <copyright file="CommentableControllerExtension.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Controllers;

public class CommentableControllerExtension
{
    private readonly IMapper mapper;
    private readonly ICommentService commentService;
    private readonly ICommentAnswerService commentAnswerService;
    private readonly PgDbContext dbContext;

    public CommentableControllerExtension(PgDbContext dbContext, IMapper mapper, ICommentService commentService, ICommentAnswerService commentAnswerService)
    {
        this.mapper = mapper;
        this.commentService = commentService;
        this.commentAnswerService = commentAnswerService;
        this.dbContext = dbContext;
    }

    public async Task<List<CommentDetailsDto>> GetCommentsForICommentable<T>(int commentableId)
        where T : BaseEntityWithId, ICommentable
    {
        if (!dbContext.Set<T>().Any(e => e.Id == commentableId))
        {
            throw new EntityNotFoundException(typeof(T).Name, commentableId.ToString());
        }

        var comments = await dbContext.Comments!.Where(c => c.CommentableId == commentableId && c.CommentableType == GetCommentableType<T>()).ToListAsync();

        var items = mapper.Map<List<CommentDetailsDto>>(comments);

        return items;
    }

    public ActionResult<List<CommentDetailsDto>> ReturnComments(List<CommentDetailsDto> items, ControllerBase controller)
    {
        items.ForEach(c =>
        {
            c.AvatarUrl = GravatarHelper.EmailToGravatarUrl(c.AuthorEmail);
        });

        if (controller.User.Identity != null && controller.User.Identity.IsAuthenticated)
        {
            return controller.Ok(items);
        }
        else
        {
            var commentsForAnonymous = mapper.Map<List<AnonymousCommentDetailsDto>>(items);

            return controller.Ok(commentsForAnonymous);
        }
    }

    public Comment CreateCommentForICommentable<T>(CommentCreateBaseDto value, int commentableId)
        where T : ICommentable
    {
        var comment = mapper.Map<Comment>(value);
        comment.CommentableId = commentableId;
        comment.CommentableType = GetCommentableType<T>();
        return comment;
    }

    public async Task<ActionResult<CommentDetailsDto>> PostComment(Comment comment, ControllerBase controller)
    {
        await commentService.SaveAsync(comment);

        // Update answer status for the new comment
        var isAuthenticated = controller.User.Identity?.IsAuthenticated ?? false;
        await commentAnswerService.UpdateAnswerStatusAsync(comment, isAuthenticated);

        await dbContext.SaveChangesAsync();

        var returnedValue = mapper.Map<CommentDetailsDto>(comment);

        return controller.CreatedAtAction("GetOne", "Comments", new { id = comment.Id }, returnedValue);
    }

    private static string GetCommentableType<T>()
        where T : ICommentable
    {
        return T.GetCommentableType();
    }
}