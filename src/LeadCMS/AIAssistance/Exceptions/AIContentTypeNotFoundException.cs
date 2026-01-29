// <copyright file="AIContentTypeNotFoundException.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Exceptions;

namespace LeadCMS.Core.AIAssistance.Exceptions;

/// <summary>
/// Exception thrown when requested content type for AI generation is not found.
/// This will return HTTP 400 Bad Request.
/// </summary>
public class AIContentTypeNotFoundException : BadRequestException
{
    public AIContentTypeNotFoundException(string contentType)
        : base($"Content type '{contentType}' not found")
    {
        AddExtension("contentType", contentType);
        AddExtension("operation", "AIContentGeneration");
    }

    public AIContentTypeNotFoundException(string contentType, Exception innerException)
        : base($"Content type '{contentType}' not found", innerException)
    {
        AddExtension("contentType", contentType);
        AddExtension("operation", "AIContentGeneration");
    }
}
