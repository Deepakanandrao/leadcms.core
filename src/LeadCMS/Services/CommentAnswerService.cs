// <copyright file="CommentAnswerService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Entities;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Services;

/// <summary>
/// Service for managing comment answer status and detection.
/// </summary>
public class CommentAnswerService : ICommentAnswerService
{
    private readonly PgDbContext dbContext;
    private readonly UserManager<User> userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommentAnswerService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="userManager">The user manager for checking registered users (legacy method).</param>
    public CommentAnswerService(PgDbContext dbContext, UserManager<User> userManager)
    {
        this.dbContext = dbContext;
        this.userManager = userManager;
    }

    /// <inheritdoc/>
    public bool IsInternalUser(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        // Check if the email belongs to a registered user in the system
        var user = userManager.FindByEmailAsync(email.Trim()).GetAwaiter().GetResult();
        return user != null;
    }

    /// <inheritdoc/>
    public async Task UpdateAnswerStatusAsync(Comment comment, bool isAuthenticated)
    {
        // Use authentication status to determine if user is internal
        var isInternalUser = isAuthenticated;

        // If this is a reply to another comment
        if (comment.ParentId.HasValue)
        {
            var parentComment = await dbContext.Comments!
                .FirstOrDefaultAsync(c => c.Id == comment.ParentId.Value);

            if (parentComment != null)
            {
                // For parent comment, we need to check if it was created by an authenticated user
                // We can determine this by checking if the parent's status is Answer
                // (since only authenticated users get these statuses automatically)
                var isParentFromInternalUser = parentComment.Status == CommentStatus.Answer;

                // If current comment is from internal user and parent is from external user
                if (isInternalUser && !isParentFromInternalUser)
                {
                    // Mark parent as answered and approved (legitimate conversation)
                    parentComment.AnswerStatus = AnswerStatus.Answered;
                    parentComment.Status = CommentStatus.Approved;

                    // Explicitly update the parent comment in the database
                    dbContext.Comments!.Update(parentComment);

                    // Mark current comment as Answer (internal response to external comment)
                    comment.AnswerStatus = AnswerStatus.Closed;
                    comment.Status = CommentStatus.Answer;
                }
                else if (!isInternalUser && isParentFromInternalUser)
                {
                    // External user replying to internal user - mark as unanswered
                    comment.AnswerStatus = AnswerStatus.Unanswered;
                }
                else
                {
                    // Same user type - inherit or set appropriately
                    comment.AnswerStatus = isInternalUser
                        ? AnswerStatus.Closed
                        : AnswerStatus.Unanswered;
                }
            }
        }
        else
        {
            // Top-level comment
            comment.AnswerStatus = isInternalUser
                ? AnswerStatus.Closed // Internal users - conversation closed
                : AnswerStatus.Unanswered; // External users need answers
        }

        // Update the comment in the database
        // Check if the entity is already being tracked
        var entry = dbContext.Entry(comment);
        if (entry.State == EntityState.Detached)
        {
            dbContext.Comments!.Update(comment);
        }

        // If already tracked, EF will automatically detect property changes
        // No need to explicitly set state to Modified
    }
}