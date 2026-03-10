// <copyright file="SegmentIdOperationFilter.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Attributes;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LeadCMS.Swagger;

/// <summary>
/// Operation filter that adds the <c>segmentId</c> parameter to Swagger documentation
/// for methods marked with <see cref="SegmentIdParameterAttribute"/>.
/// </summary>
public class SegmentIdOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var attribute = context.MethodInfo
            .GetCustomAttributes(typeof(SegmentIdParameterAttribute), false)
            .FirstOrDefault() as SegmentIdParameterAttribute;

        if (attribute == null)
        {
            return;
        }

        operation.Parameters ??= new List<OpenApiParameter>();

        var existingParam = operation.Parameters.FirstOrDefault(p => p.Name == "segmentId");
        if (existingParam != null)
        {
            return;
        }

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "segmentId",
            In = ParameterLocation.Query,
            Description = attribute.Description,
            Required = false,
            Schema = new OpenApiSchema
            {
                Type = "integer",
                Format = "int32",
            },
        });
    }
}
