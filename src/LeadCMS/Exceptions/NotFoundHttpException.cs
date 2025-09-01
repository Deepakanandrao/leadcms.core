// <copyright file="NotFoundHttpException.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Exceptions.Base;

namespace LeadCMS.Exceptions;

/// <summary>
/// Base class for exceptions that should return HTTP 404 Not Found.
/// </summary>
public class NotFoundHttpException : BaseHttpException
{
    public NotFoundHttpException(string message)
        : base(message)
    {
    }

    public NotFoundHttpException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public override int StatusCode => StatusCodes.Status404NotFound;
}
