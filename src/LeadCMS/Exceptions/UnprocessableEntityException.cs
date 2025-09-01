// <copyright file="UnprocessableEntityException.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Exceptions.Base;

namespace LeadCMS.Exceptions;

/// <summary>
/// Base class for exceptions that should return HTTP 422 Unprocessable Entity.
/// </summary>
public class UnprocessableEntityException : BaseHttpException
{
    public UnprocessableEntityException(string message)
        : base(message)
    {
    }

    public UnprocessableEntityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public override int StatusCode => StatusCodes.Status422UnprocessableEntity;
}
