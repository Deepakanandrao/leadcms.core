// <copyright file="SendGridApiException.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Exceptions;

namespace LeadCMS.Plugin.SendGrid.Exceptions;

public class SendGridApiException : InternalServerErrorException
{
    public SendGridApiException(string message)
        : base(message)
    {
        AddExtension("provider", "SendGrid");
        AddExtension("category", "EmailAPI");
    }

    public SendGridApiException(string message, Exception innerException)
        : base(message, innerException)
    {
        AddExtension("provider", "SendGrid");
        AddExtension("category", "EmailAPI");
    }
}