// <copyright file="DuplicateRedirectSourceException.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Exceptions.Base;

namespace LeadCMS.Exceptions;

public class DuplicateRedirectSourceException : BaseHttpException
{
    public DuplicateRedirectSourceException(string message)
        : base(message)
    {
    }

    public override int StatusCode => StatusCodes.Status409Conflict;
}
