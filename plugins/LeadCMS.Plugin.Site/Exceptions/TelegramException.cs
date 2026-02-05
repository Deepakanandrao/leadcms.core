// <copyright file="TelegramException.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Plugin.Site.Exceptions;

/// <summary>
/// Exception thrown when a Telegram API operation fails.
/// </summary>
public class TelegramException : Exception
{
    public TelegramException()
    {
    }

    public TelegramException(string? message)
        : base(message)
    {
    }

    public TelegramException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
