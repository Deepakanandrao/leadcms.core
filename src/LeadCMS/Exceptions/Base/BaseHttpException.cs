// <copyright file="BaseHttpException.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Exceptions.Interfaces;

namespace LeadCMS.Exceptions.Base;

/// <summary>
/// Base class for exceptions that can specify their HTTP status code and additional details.
/// </summary>
public abstract class BaseHttpException : Exception, IHttpStatusException
{
    private readonly Dictionary<string, object?> extensions = new();

    protected BaseHttpException(string message)
        : base(message)
    {
    }

    protected BaseHttpException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Gets the HTTP status code that should be returned for this exception.
    /// </summary>
    public abstract int StatusCode { get; }

    /// <summary>
    /// Gets additional details to include in the problem details response.
    /// </summary>
    public virtual IDictionary<string, object?> GetExtensions()
    {
        return extensions;
    }

    /// <summary>
    /// Adds an extension value to be included in the problem details response.
    /// </summary>
    /// <param name="key">The extension key.</param>
    /// <param name="value">The extension value.</param>
    protected void AddExtension(string key, object? value)
    {
        extensions[key] = value;
    }
}
