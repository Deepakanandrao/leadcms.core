// <copyright file="AIContentNotGeneratedException.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Exceptions;

namespace LeadCMS.Core.AIAssistance.Exceptions;

/// <summary>
/// Exception thrown when AI fails to generate content as requested.
/// This will return HTTP 422 Unprocessable Entity.
/// </summary>
public class AIContentNotGeneratedException : UnprocessableEntityException
{
    public AIContentNotGeneratedException(string reason)
        : base($"AI could not generate content: {reason}")
    {
        AddExtension("reason", reason);
        AddExtension("category", "ContentGeneration");
    }

    public AIContentNotGeneratedException(string reason, Exception innerException)
        : base($"AI could not generate content: {reason}", innerException)
    {
        AddExtension("reason", reason);
        AddExtension("category", "ContentGeneration");
    }
}
