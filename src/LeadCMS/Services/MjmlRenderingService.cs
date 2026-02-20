// <copyright file="MjmlRenderingService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Interfaces;
using Mjml.Net;

namespace LeadCMS.Services;

/// <summary>
/// Renders MJML markup into HTML using the Mjml.Net library.
/// </summary>
public class MjmlRenderingService : IMjmlRenderingService
{
    private readonly MjmlRenderer renderer;

    public MjmlRenderingService()
    {
        renderer = new MjmlRenderer();
    }

    /// <inheritdoc/>
    public string RenderToHtml(string mjml)
    {
        if (string.IsNullOrWhiteSpace(mjml))
        {
            return mjml;
        }

        var result = renderer.Render(mjml, new MjmlOptions
        {
            Beautify = false,
        });

        if (result.Errors?.Count > 0)
        {
            var errorMessages = string.Join("; ", result.Errors.Select(e => $"Line {e.Position.LineNumber}: {e.Error}"));
            Log.Warning("MJML rendering produced errors: {Errors}", errorMessages);
        }

        return result.Html;
    }
}
