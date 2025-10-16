// <copyright file="ICommentAnswerService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;

namespace LeadCMS.Interfaces;

/// <summary>
/// Service for managing comment answer status and detection.
/// </summary>
public interface ICommentAnswerService
{
    /// <summary>
    /// Determines if a user is considered internal based on their email.
    /// </summary>
    /// <param name="email">The user's email address.</param>
    /// <returns>True if the user is internal, false otherwise.</returns>
    bool IsInternalUser(string email);

    /// <summary>
    /// Updates the answer status of a comment based on automatic detection rules.
    /// This method analyzes the comment hierarchy and authentication status to determine answer status.
    /// </summary>
    /// <param name="comment">The comment to analyze.</param>
    /// <param name="isAuthenticated">Whether the comment author is authenticated.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAnswerStatusAsync(Comment comment, bool isAuthenticated);
}