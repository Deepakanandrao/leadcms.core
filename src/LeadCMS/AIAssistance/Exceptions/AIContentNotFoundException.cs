// <copyright file="AIContentNotFoundException.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Exceptions;

namespace LeadCMS.Core.AIAssistance.Exceptions;

/// <summary>
/// Exception thrown when requested content for AI editing is not found.
/// This will return HTTP 404 Not Found.
/// </summary>
public class AIContentNotFoundException : NotFoundHttpException
{
    public AIContentNotFoundException(int contentId)
        : base($"Content with ID {contentId} not found for AI editing")
    {
        AddExtension("contentId", contentId);
        AddExtension("operation", "AIContentEdit");
    }

    public AIContentNotFoundException(int contentId, Exception innerException)
        : base($"Content with ID {contentId} not found for AI editing", innerException)
    {
        AddExtension("contentId", contentId);
        AddExtension("operation", "AIContentEdit");
    }
}
