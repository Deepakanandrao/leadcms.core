// <copyright file="ConflictException.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Exceptions.Base;

namespace LeadCMS.Exceptions;

/// <summary>
/// Base class for exceptions that should return HTTP 409 Conflict.
/// </summary>
public class ConflictException : BaseHttpException
{
    public ConflictException(string message)
        : base(message)
    {
    }

    public ConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public override int StatusCode => StatusCodes.Status409Conflict;
}
