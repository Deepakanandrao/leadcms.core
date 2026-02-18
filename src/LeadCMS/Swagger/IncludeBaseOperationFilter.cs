// <copyright file="IncludeBaseOperationFilter.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Attributes;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LeadCMS.Swagger;

/// <summary>
/// Operation filter that adds the includeBase parameter to Swagger documentation
/// for methods marked with IncludeBaseParameterAttribute.
/// </summary>
public class IncludeBaseOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var attribute = context.MethodInfo
            .GetCustomAttributes(typeof(IncludeBaseParameterAttribute), false)
            .FirstOrDefault() as IncludeBaseParameterAttribute;

        if (attribute != null)
        {
            operation.Parameters ??= new List<OpenApiParameter>();

            // Check if parameter already exists to avoid duplicates
            var existingParam = operation.Parameters.FirstOrDefault(p => p.Name == "includeBase");
            if (existingParam == null)
            {
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "includeBase",
                    In = ParameterLocation.Query,
                    Description = attribute.Description,
                    Required = false,
                    Schema = new OpenApiSchema
                    {
                        Type = "boolean",
                        Default = new Microsoft.OpenApi.Any.OpenApiBoolean(false),
                    },
                });
            }
        }
    }
}
