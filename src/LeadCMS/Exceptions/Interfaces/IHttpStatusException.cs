// <copyright file="IHttpStatusException.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Exceptions.Interfaces;

/// <summary>
/// Defines an exception that can specify its HTTP status code for error responses.
/// </summary>
public interface IHttpStatusException
{
    /// <summary>
    /// Gets the HTTP status code that should be returned for this exception.
    /// </summary>
    int StatusCode { get; }

    /// <summary>
    /// Gets additional details to include in the problem details response.
    /// </summary>
    /// <returns>A dictionary containing additional details to include in the problem details response.</returns>
    IDictionary<string, object?> GetExtensions();
}
