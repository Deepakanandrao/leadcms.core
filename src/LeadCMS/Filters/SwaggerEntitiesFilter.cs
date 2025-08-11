// <copyright file="SwaggerEntitiesFilter.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Reflection;
using LeadCMS.Configuration;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LeadCMS.Filters
{
    public class SwaggerEntitiesFilter : IDocumentFilter
    {
        private readonly List<string> includedEntities;
        private readonly List<string> excludedEntities;
        private readonly List<Type> currentTypes;
        private readonly HashSet<string> excludedSchemaKeys = new();

        public SwaggerEntitiesFilter(EntitiesConfig config)
        {
            currentTypes = Assembly.GetExecutingAssembly().GetTypes().ToList();
            includedEntities = excludedEntities = new List<string>();
            if (config != null)
            {
                includedEntities = config.Include.ToList();
                excludedEntities = config.Exclude.ToList();
            }
        }

        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            if (includedEntities.Count == 0 && excludedEntities.Count == 0)
            {
                return; // No entities to filter, skip processing
            }

            var schemas = context.SchemaRepository.Schemas;            

            // First pass: identify and remove excluded schemas
            var excludeSchemas = schemas.Where(s => SchemaNeedsToBeExcluded(s.Key.ToString().ToLower())).Select(s => s.Key).ToList();
            foreach (var schemaKey in excludeSchemas)
            {
                excludedSchemaKeys.Add(schemaKey);
                schemas.Remove(schemaKey);
            }

            // Second pass: clean up references to excluded schemas in remaining schemas
            CleanupSchemaReferences(schemas);

            // Third pass: clean up operation request/response schemas and remove problematic paths (context aware)
            CleanupOperations(swaggerDoc, context);

            // Remove excluded paths
            var excludePaths = swaggerDoc.Paths.Where(p => OperationNeedsToBeExcluded(context, p.Key)).ToList();
            foreach (var path in excludePaths)
            {
                swaggerDoc.Paths.Remove(path.Key);
            }
        }

        private bool SchemaNeedsToBeExcluded(string key)
        {
            var included = includedEntities.Count == 0 || includedEntities.Exists(s => key.Contains(s, StringComparison.OrdinalIgnoreCase));
            var excluded = includedEntities.Count == 0 && excludedEntities.Exists(s => key.Contains(s, StringComparison.OrdinalIgnoreCase));
            var current = currentTypes.Exists(t => string.Equals(t.Name, key, StringComparison.OrdinalIgnoreCase));
            return current && (!included || excluded);
        }

        private bool OperationNeedsToBeExcluded(DocumentFilterContext context, string path)
        {
            // Create a precise mapping of entities to their exact controller paths (case-insensitive)
            var entityToControllerMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "user", "/api/users" },
                { "setting", "/api/settings" },
                { "redirect", "/api/redirects" },
                { "deal", "/api/deals" },
                { "comment", "/api/comments" },
                { "contact", "/api/contacts" },
                { "content", "/api/content" },
                { "contenttype", "/api/content-types" },
                { "link", "/api/links" },
                { "account", "/api/accounts" },
                { "unsubscribe", "/api/unsubscribes" },
                { "domain", "/api/domains" },
                { "activity-log", "/api/activity-logs" },
                { "changelog", "/api/changelogs" },
                { "changelogtask", "/api/changelog-tasks" },
                { "contactemailschedule", "/api/contact-email-schedules" },
                { "dealpipeline", "/api/deal-pipelines" },
                { "dealpipelinestage", "/api/deal-pipeline-stages" },
                { "discount", "/api/discounts" },
                { "emailgroup", "/api/email-groups" },
                { "emailtemplate", "/api/email-templates" },
                { "file", "/api/files" },
                { "media", "/api/media" },
                { "order", "/api/orders" },
                { "changelogtasklog", "/api/changelog-task-logs" },
                { "emaillog", "/api/email-logs" },
                { "emailschedule", "/api/email-schedules" },
                { "ipdetails", "/api/ip-details" },
                { "linklog", "/api/link-logs" },
                { "mailserver", "/api/mail-servers" },
                { "orderitem", "/api/order-items" },
                { "promotion", "/api/promotions" },
                { "taskexecutionlog", "/api/task-execution-logs" },
                { "sendgridevent", "/api/sendgrid-events" },
                { "task", "/api/tasks" },
                { "dashboard", "/api/dashboard" },
            };

            // Find which entity this path corresponds to
            var matchedEntity = entityToControllerMap
                .Where(entityMapping => path.StartsWith(entityMapping.Value, StringComparison.OrdinalIgnoreCase))
                .Select(entityMapping => entityMapping.Key)
                .FirstOrDefault();

            // If we found a matching entity, apply include/exclude logic
            if (matchedEntity != null)
            {
                // If there are included entities, only include those specified
                if (includedEntities.Count > 0)
                {
                    var isIncluded = includedEntities.Exists(e => string.Equals(e, matchedEntity, StringComparison.OrdinalIgnoreCase));
                    if (isIncluded)
                    {
                        return false; // Keep if in the include list
                    }
                }

                // Check if explicitly excluded
                var isExcluded = excludedEntities.Exists(e => string.Equals(e, matchedEntity, StringComparison.OrdinalIgnoreCase));
                if (isExcluded)
                {
                    return true; // Exclude if in exclude list
                }
            }

            // Original logic for plugin exclusion - keep plugins regardless of include/exclude lists
            var api = context.ApiDescriptions.FirstOrDefault(d => d != null && d.RelativePath != null && path == '/' + d.RelativePath);
            var keepFromPlugins = api != null && api.ActionDescriptor.DisplayName != null && api.ActionDescriptor.DisplayName.Contains("Plugin");
            
            return !keepFromPlugins; // Don't exclude plugins
        }

        private void CleanupSchemaReferences(IDictionary<string, OpenApiSchema> schemas)
        {
            foreach (var schema in schemas.Values)
            {
                CleanupSchemaProperties(schema);
            }
        }

        private void CleanupSchemaProperties(OpenApiSchema schema)
        {
            if (schema.Properties == null)
            {
                return;
            }

            var propertiesToRemove = new List<string>();

            foreach (var property in schema.Properties)
            {
                if (ShouldRemoveProperty(property.Value))
                {
                    propertiesToRemove.Add(property.Key);
                }
                else
                {
                    // Recursively clean nested schemas
                    CleanupSchemaProperties(property.Value);
                }
            }

            // Remove properties that reference excluded schemas
            foreach (var propertyKey in propertiesToRemove)
            {
                schema.Properties.Remove(propertyKey);
            }

            // Clean up AllOf, AnyOf, OneOf references
            CleanupSchemaComposition(schema);
        }

        private bool ShouldRemoveProperty(OpenApiSchema propertySchema)
        {
            // Check direct reference
            if (propertySchema.Reference != null)
            {
                var refId = ExtractSchemaIdFromReference(propertySchema.Reference.Id);
                if (excludedSchemaKeys.Contains(refId))
                {
                    return true;
                }
            }

            // Check array items reference
            if (propertySchema.Type == "array" && propertySchema.Items?.Reference != null)
            {
                var refId = ExtractSchemaIdFromReference(propertySchema.Items.Reference.Id);
                if (excludedSchemaKeys.Contains(refId))
                {
                    return true;
                }
            }

            return false;
        }

        private void CleanupSchemaComposition(OpenApiSchema schema)
        {
            // Clean AllOf
            if (schema.AllOf != null)
            {
                var itemsToRemove = schema.AllOf.Where(s => s.Reference != null && 
                    excludedSchemaKeys.Contains(ExtractSchemaIdFromReference(s.Reference.Id))).ToList();
                foreach (var item in itemsToRemove)
                {
                    schema.AllOf.Remove(item);
                }
            }

            // Clean AnyOf
            if (schema.AnyOf != null)
            {
                var itemsToRemove = schema.AnyOf.Where(s => s.Reference != null && 
                    excludedSchemaKeys.Contains(ExtractSchemaIdFromReference(s.Reference.Id))).ToList();
                foreach (var item in itemsToRemove)
                {
                    schema.AnyOf.Remove(item);
                }
            }

            // Clean OneOf
            if (schema.OneOf != null)
            {
                var itemsToRemove = schema.OneOf.Where(s => s.Reference != null && 
                    excludedSchemaKeys.Contains(ExtractSchemaIdFromReference(s.Reference.Id))).ToList();
                foreach (var item in itemsToRemove)
                {
                    schema.OneOf.Remove(item);
                }
            }
        }

        private void CleanupOperations(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            var pathsToRemove = new List<string>();

            foreach (var path in swaggerDoc.Paths)
            {
                var hasInvalidRefs = false;

                foreach (var operation in path.Value.Operations.Values)
                {
                    // Check request body schemas
                    if (operation.RequestBody?.Content != null)
                    {
                        var invalidContentTypes = operation.RequestBody.Content
                            .Where(kvp => HasInvalidSchemaReference(kvp.Value.Schema))
                            .Select(kvp => kvp.Key)
                            .ToList();

                        if (invalidContentTypes.Count > 0)
                        {
                            hasInvalidRefs = true;
                            // Prune invalid media types instead of marking whole path for removal (only if path is otherwise included)
                            foreach (var ct in invalidContentTypes)
                            {
                                operation.RequestBody.Content.Remove(ct);
                            }
                        }

                        // Continue evaluating responses to prune them too; do not break early so we can clean
                    }

                    // Check response schemas
                    if (operation.Responses != null)
                    {
                        foreach (var responseContent in operation.Responses.Values
                            .Where(r => r.Content != null)
                            .Select(r => r.Content))
                        {
                            var invalidResponseContentTypes = responseContent
                                .Where(kvp => HasInvalidSchemaReference(kvp.Value.Schema))
                                .Select(kvp => kvp.Key)
                                .ToList();
                            if (invalidResponseContentTypes.Count > 0)
                            {
                                hasInvalidRefs = true;
                                foreach (var ct in invalidResponseContentTypes)
                                {
                                    responseContent.Remove(ct);
                                }
                            }
                        }
                    }
                }

                if (hasInvalidRefs && OperationNeedsToBeExcluded(context, path.Key))
                {
                    // Only remove path if it is NOT explicitly included (per OperationNeedsToBeExcluded logic)
                    pathsToRemove.Add(path.Key);
                }
            }

            // Remove paths with invalid references
            foreach (var pathKey in pathsToRemove)
            {
                swaggerDoc.Paths.Remove(pathKey);
            }
        }

        private bool HasInvalidSchemaReference(OpenApiSchema schema)
        {
            if (schema == null)
            {
                return false;
            }

            // Check direct reference
            if (schema.Reference != null)
            {
                var refId = ExtractSchemaIdFromReference(schema.Reference.Id);
                if (excludedSchemaKeys.Contains(refId))
                {
                    return true;
                }
            }

            // Check array items
            if (schema.Type == "array" && schema.Items != null)
            {
                return HasInvalidSchemaReference(schema.Items);
            }

            // Check additional properties
            if (schema.AdditionalProperties != null)
            {
                return HasInvalidSchemaReference(schema.AdditionalProperties);
            }

            // Check properties
            if (schema.Properties != null && schema.Properties.Values.Any(property => HasInvalidSchemaReference(property)))
            {
                return true;
            }

            // Check composition schemas
            if (schema.AllOf != null && schema.AllOf.Any(subSchema => HasInvalidSchemaReference(subSchema)))
            {
                return true;
            }

            if (schema.AnyOf != null && schema.AnyOf.Any(subSchema => HasInvalidSchemaReference(subSchema)))
            {
                return true;
            }

            if (schema.OneOf != null && schema.OneOf.Any(subSchema => HasInvalidSchemaReference(subSchema)))
            {
                return true;
            }

            return false;
        }

        private string ExtractSchemaIdFromReference(string referenceId)
        {
            // Reference ID typically comes in format like "#/components/schemas/ContactDetailsDto"
            // We want to extract just "ContactDetailsDto"
            if (referenceId.Contains("/"))
            {
                return referenceId.Split('/').Last();
            }

            return referenceId;
        }
    }
}