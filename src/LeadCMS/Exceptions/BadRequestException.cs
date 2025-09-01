// <copyright file="BadRequestException.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Exceptions.Base;

namespace LeadCMS.Exceptions;

/// <summary>
/// Base class for exceptions that should return HTTP 400 Bad Request.
/// </summary>
public class BadRequestException : BaseHttpException
{
    public BadRequestException(string message)
        : base(message)
    {
    }

    public BadRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public override int StatusCode => StatusCodes.Status400BadRequest;
}
