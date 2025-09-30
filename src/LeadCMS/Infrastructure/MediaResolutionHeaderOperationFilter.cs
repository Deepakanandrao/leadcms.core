// <copyright file="MediaResolutionHeaderOperationFilter.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LeadCMS.Infrastructure;

/// <summary>
/// Adds the X-Media-Resolution header parameter to Swagger UI for relevant endpoints.
/// </summary>
public class MediaResolutionHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Only add if not already present
        if (operation.Parameters == null)
        {
            operation.Parameters = new List<OpenApiParameter>();
        }

        // Only add to /api/content and /api/media GET endpoints (and POST/PATCH for media)
        var controllerName = context.ApiDescription.ActionDescriptor.RouteValues["controller"];
        var httpMethod = context.ApiDescription.HttpMethod;

        var isContentController = string.Equals(controllerName, "Content", StringComparison.OrdinalIgnoreCase);
        var isMediaController = string.Equals(controllerName, "Media", StringComparison.OrdinalIgnoreCase);
        var isGetMethod = string.Equals(httpMethod, "GET", StringComparison.OrdinalIgnoreCase);

        if (!((isContentController && isGetMethod) || (isMediaController && isGetMethod)))
        {
            return;
        }

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Media-Resolution",
            In = ParameterLocation.Header,
            Description = "Set to 'absolute' to resolve media URLs as absolute URLs. Default is 'relative'.",
            Required = false,
            Schema = new OpenApiSchema { Type = "string", Default = new Microsoft.OpenApi.Any.OpenApiString("relative") },
        });
    }
}
