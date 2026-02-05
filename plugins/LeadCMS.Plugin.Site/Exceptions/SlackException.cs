// <copyright file="SlackException.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Plugin.Site.Exceptions;

/// <summary>
/// Exception thrown when a Slack API operation fails.
/// </summary>
public class SlackException : Exception
{
    public SlackException()
    {
    }

    public SlackException(string? message)
        : base(message)
    {
    }

    public SlackException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
