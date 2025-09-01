// <copyright file="InternalServerErrorException.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Exceptions.Base;

namespace LeadCMS.Exceptions;

/// <summary>
/// Base class for exceptions that should return HTTP 500 Internal Server Error.
/// </summary>
public class InternalServerErrorException : BaseHttpException
{
    public InternalServerErrorException(string message)
        : base(message)
    {
    }

    public InternalServerErrorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public override int StatusCode => StatusCodes.Status500InternalServerError;
}
