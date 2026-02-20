// <copyright file="IMjmlRenderingService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Interfaces;

/// <summary>
/// Service for rendering MJML markup into HTML suitable for email clients.
/// </summary>
public interface IMjmlRenderingService
{
    /// <summary>
    /// Renders MJML markup to HTML.
    /// </summary>
    /// <param name="mjml">The MJML markup string.</param>
    /// <returns>The rendered HTML string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when MJML rendering produces errors.</exception>
    string RenderToHtml(string mjml);
}
