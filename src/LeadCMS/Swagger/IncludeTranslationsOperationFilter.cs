// <copyright file="IncludeTranslationsOperationFilter.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Attributes;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LeadCMS.Swagger;

/// <summary>
/// Operation filter that adds the includeTranslations parameter to Swagger documentation
/// for methods marked with IncludeTranslationsParameterAttribute.
/// </summary>
public class IncludeTranslationsOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasIncludeTranslationsAttribute = context.MethodInfo
            .GetCustomAttributes(typeof(IncludeTranslationsParameterAttribute), false)
            .FirstOrDefault() as IncludeTranslationsParameterAttribute;

        if (hasIncludeTranslationsAttribute != null)
        {
            operation.Parameters ??= new List<OpenApiParameter>();

            // Check if parameter already exists to avoid duplicates
            var existingParam = operation.Parameters.FirstOrDefault(p => p.Name == "includeTranslations");
            if (existingParam == null)
            {
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "includeTranslations",
                    In = ParameterLocation.Query,
                    Description = hasIncludeTranslationsAttribute.Description,
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
