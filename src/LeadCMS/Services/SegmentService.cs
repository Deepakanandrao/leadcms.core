// <copyright file="SegmentService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Linq.Expressions;
using AutoMapper;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Services;

public class SegmentService : ISegmentService
{
    private readonly PgDbContext dbContext;
    private readonly IMapper mapper;

    public SegmentService(PgDbContext dbContext, IMapper mapper)
    {
        this.dbContext = dbContext;
        this.mapper = mapper;
    }

    public async Task<int> CalculateContactCountAsync(Segment segment)
    {
        if (segment.Type == "static")
        {
            return segment.ContactIds?.Length ?? 0;
        }

        if (segment.Type == "dynamic" && segment.Definition != null)
        {
            var definition = System.Text.Json.JsonSerializer.Deserialize<SegmentDefinition>(segment.Definition);
            if (definition != null)
            {
                var contacts = await EvaluateDynamicSegmentAsync(definition);
                return contacts.Count;
            }
        }

        return 0;
    }

    public async Task<List<Contact>> GetSegmentContactsAsync(int segmentId, string? query = null, int? limit = null)
    {
        var segment = await dbContext.Segments!
            .Where(s => s.Id == segmentId)
            .FirstOrDefaultAsync();

        if (segment == null)
        {
            throw new EntityNotFoundException("Segment", segmentId.ToString());
        }

        List<Contact> contacts;

        if (segment.Type == "static")
        {
            var contactIds = segment.ContactIds ?? Array.Empty<int>();
            var contactsQuery = dbContext.Contacts!.Where(c => contactIds.Contains(c.Id));

            if (!string.IsNullOrEmpty(query))
            {
                var lowerQuery = query.ToLower();
                contactsQuery = contactsQuery.Where(c =>
                    c.Email.ToLower().Contains(lowerQuery) ||
                    (c.FirstName != null && c.FirstName.ToLower().Contains(lowerQuery)) ||
                    (c.LastName != null && c.LastName.ToLower().Contains(lowerQuery)));
            }

            if (limit.HasValue)
            {
                contactsQuery = contactsQuery.Take(limit.Value);
            }

            contacts = await contactsQuery.ToListAsync();
        }
        else if (segment.Type == "dynamic" && segment.Definition != null)
        {
            var definition = System.Text.Json.JsonSerializer.Deserialize<SegmentDefinition>(segment.Definition);
            if (definition == null)
            {
                return new List<Contact>();
            }

            contacts = await EvaluateDynamicSegmentAsync(definition, limit);

            if (!string.IsNullOrEmpty(query))
            {
                var lowerQuery = query.ToLower();
                contacts = contacts.Where(c =>
                    c.Email.ToLower().Contains(lowerQuery) ||
                    (c.FirstName != null && c.FirstName.ToLower().Contains(lowerQuery)) ||
                    (c.LastName != null && c.LastName.ToLower().Contains(lowerQuery))).ToList();
            }
        }
        else
        {
            contacts = new List<Contact>();
        }

        return contacts;
    }

    public async Task<SegmentPreviewResultDto> PreviewSegmentAsync(SegmentDefinition definition, int limit = 100)
    {
        var contacts = await EvaluateDynamicSegmentAsync(definition, limit);

        var contactDtos = mapper.Map<List<ContactDetailsDto>>(contacts);
        contactDtos.ForEach(c =>
        {
            c.AvatarUrl = GravatarHelper.EmailToGravatarUrl(c.Email);
        });

        return new SegmentPreviewResultDto
        {
            ContactCount = contacts.Count,
            Contacts = contactDtos,
        };
    }

    public async Task<List<Contact>> EvaluateDynamicSegmentAsync(SegmentDefinition definition, int? limit = null)
    {
        var query = dbContext.Contacts!.AsQueryable();

        // Apply include rules
        if (definition.IncludeRules != null)
        {
            var includeExpression = BuildRuleGroupExpression(definition.IncludeRules);
            if (includeExpression != null)
            {
                query = query.Where(includeExpression);
            }
        }

        // Apply exclude rules
        if (definition.ExcludeRules != null)
        {
            var excludeExpression = BuildRuleGroupExpression(definition.ExcludeRules);
            if (excludeExpression != null)
            {
                var notExpression = Expression.Lambda<Func<Contact, bool>>(
                    Expression.Not(excludeExpression.Body),
                    excludeExpression.Parameters);
                query = query.Where(notExpression);
            }
        }

        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        return await query.ToListAsync();
    }

    public async Task ValidateSegmentAsync(Segment segment)
    {
        // Check for unique name
        var existingSegment = await dbContext.Segments!
            .Where(s => s.Name == segment.Name && s.Id != segment.Id)
            .FirstOrDefaultAsync();

        if (existingSegment != null)
        {
            throw new InvalidOperationException($"A segment with the name '{segment.Name}' already exists.");
        }

        // Validate dynamic segments have definition
        if (segment.Type == "dynamic" && string.IsNullOrEmpty(segment.Definition))
        {
            throw new InvalidOperationException("Dynamic segments must have a definition with at least one include rule.");
        }

        // Validate static segments
        if (segment.Type == "static" && segment.ContactIds != null && segment.ContactIds.Length > 0)
        {
            // Validate that all contact IDs exist
            var contactIds = segment.ContactIds;
            var existingContactIds = await dbContext.Contacts!
                .Where(c => contactIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync();

            var invalidIds = contactIds.Except(existingContactIds).ToArray();
            if (invalidIds.Length > 0)
            {
                throw new InvalidOperationException($"The following contact IDs do not exist: {string.Join(", ", invalidIds)}");
            }
        }
    }

    private Expression<Func<Contact, bool>>? BuildRuleGroupExpression(RuleGroup ruleGroup)
    {
        var expressions = new List<Expression<Func<Contact, bool>>>();

        // Process individual rules
        foreach (var rule in ruleGroup.Rules)
        {
            var ruleExpression = BuildRuleExpression(rule);
            if (ruleExpression != null)
            {
                expressions.Add(ruleExpression);
            }
        }

        // Process nested groups
        foreach (var group in ruleGroup.Groups)
        {
            var groupExpression = BuildRuleGroupExpression(group);
            if (groupExpression != null)
            {
                expressions.Add(groupExpression);
            }
        }

        if (expressions.Count == 0)
        {
            return null;
        }

        // Combine expressions based on connector
        var parameter = Expression.Parameter(typeof(Contact), "c");
        Expression? combinedExpression = null;

        foreach (var expr in expressions)
        {
            var invokedExpr = Expression.Invoke(expr, parameter);
            if (combinedExpression == null)
            {
                combinedExpression = invokedExpr;
            }
            else if (ruleGroup.Connector == RuleConnector.And)
            {
                combinedExpression = Expression.AndAlso(combinedExpression, invokedExpr);
            }
            else
            {
                combinedExpression = Expression.OrElse(combinedExpression, invokedExpr);
            }
        }

        return combinedExpression == null ? null : Expression.Lambda<Func<Contact, bool>>(combinedExpression, parameter);
    }

    private Expression<Func<Contact, bool>>? BuildRuleExpression(SegmentRule rule)
    {
        var parameter = Expression.Parameter(typeof(Contact), "c");
        var property = GetPropertyExpression(parameter, rule.FieldId);

        if (property == null)
        {
            return null;
        }

        Expression? comparison = null;

        try
        {
            switch (rule.Operator)
            {
                case FieldOperator.Equals:
                    comparison = BuildEqualsExpression(property, rule.Value);
                    break;
                case FieldOperator.NotEquals:
                    comparison = BuildNotEqualsExpression(property, rule.Value);
                    break;
                case FieldOperator.Contains:
                    comparison = BuildContainsExpression(property, rule.Value);
                    break;
                case FieldOperator.NotContains:
                    comparison = BuildNotContainsExpression(property, rule.Value);
                    break;
                case FieldOperator.StartsWith:
                    comparison = BuildStartsWithExpression(property, rule.Value);
                    break;
                case FieldOperator.EndsWith:
                    comparison = BuildEndsWithExpression(property, rule.Value);
                    break;
                case FieldOperator.IsEmpty:
                    comparison = BuildIsEmptyExpression(property);
                    break;
                case FieldOperator.IsNotEmpty:
                    comparison = BuildIsNotEmptyExpression(property);
                    break;
                case FieldOperator.GreaterThan:
                    comparison = BuildGreaterThanExpression(property, rule.Value);
                    break;
                case FieldOperator.LessThan:
                    comparison = BuildLessThanExpression(property, rule.Value);
                    break;
                case FieldOperator.GreaterThanOrEqual:
                    comparison = BuildGreaterThanOrEqualExpression(property, rule.Value);
                    break;
                case FieldOperator.LessThanOrEqual:
                    comparison = BuildLessThanOrEqualExpression(property, rule.Value);
                    break;
                case FieldOperator.IsTrue:
                    if (property.Type == typeof(bool) || property.Type == typeof(bool?))
                    {
                        comparison = Expression.Equal(property, Expression.Constant(true, property.Type));
                    }

                    break;
                case FieldOperator.IsFalse:
                    if (property.Type == typeof(bool) || property.Type == typeof(bool?))
                    {
                        comparison = Expression.Equal(property, Expression.Constant(false, property.Type));
                    }

                    break;
                case FieldOperator.In:
                    comparison = BuildInExpression(property, rule.Value);
                    break;
                case FieldOperator.NotIn:
                    comparison = BuildNotInExpression(property, rule.Value);
                    break;
            }
        }
        catch (Exception)
        {
            // If expression building fails due to type mismatch or other issues, skip this rule
            return null;
        }

        return comparison == null ? null : Expression.Lambda<Func<Contact, bool>>(comparison, parameter);
    }

    private Expression? GetPropertyExpression(ParameterExpression parameter, string fieldId)
    {
        try
        {
            // Map fieldId to Contact property
            var propertyName = fieldId switch
            {
                "email" => "Email",
                "firstName" => "FirstName",
                "lastName" => "LastName",
                "tags" => "Tags",
                "createdAt" => "CreatedAt",
                "updatedAt" => "UpdatedAt",
                "country" => "CountryCode",
                "city" => "CityName",
                "account" => "AccountId",
                "phone" => "Phone",
                "companyName" => "CompanyName",
                "jobTitle" => "JobTitle",
                _ => fieldId,
            };

            return Expression.Property(parameter, propertyName);
        }
        catch (ArgumentException)
        {
            // Property doesn't exist on Contact entity
            return null;
        }
    }

    private Expression BuildEqualsExpression(Expression property, object? value)
    {
        var constantValue = Expression.Constant(ConvertValue(value, property.Type), property.Type);
        return Expression.Equal(property, constantValue);
    }

    private Expression BuildNotEqualsExpression(Expression property, object? value)
    {
        var constantValue = Expression.Constant(ConvertValue(value, property.Type), property.Type);
        return Expression.NotEqual(property, constantValue);
    }

    private Expression BuildContainsExpression(Expression property, object? value)
    {
        if (property.Type == typeof(string))
        {
            var nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
            var method = typeof(string).GetMethod("Contains", new[] { typeof(string) })!;
            var containsCall = Expression.Call(property, method, Expression.Constant(value?.ToString() ?? string.Empty));
            return Expression.AndAlso(nullCheck, containsCall);
        }

        return Expression.Constant(false);
    }

    private Expression BuildNotContainsExpression(Expression property, object? value)
    {
        if (property.Type == typeof(string))
        {
            var isNull = Expression.Equal(property, Expression.Constant(null, typeof(string)));
            var method = typeof(string).GetMethod("Contains", new[] { typeof(string) })!;
            var notContainsCall = Expression.Not(Expression.Call(property, method, Expression.Constant(value?.ToString() ?? string.Empty)));
            return Expression.OrElse(isNull, notContainsCall);
        }

        return Expression.Constant(true);
    }

    private Expression BuildStartsWithExpression(Expression property, object? value)
    {
        if (property.Type == typeof(string))
        {
            var nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
            var method = typeof(string).GetMethod("StartsWith", new[] { typeof(string) })!;
            var startsWithCall = Expression.Call(property, method, Expression.Constant(value?.ToString() ?? string.Empty));
            return Expression.AndAlso(nullCheck, startsWithCall);
        }

        return Expression.Constant(false);
    }

    private Expression BuildEndsWithExpression(Expression property, object? value)
    {
        if (property.Type == typeof(string))
        {
            var nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
            var method = typeof(string).GetMethod("EndsWith", new[] { typeof(string) })!;
            var endsWithCall = Expression.Call(property, method, Expression.Constant(value?.ToString() ?? string.Empty));
            return Expression.AndAlso(nullCheck, endsWithCall);
        }

        return Expression.Constant(false);
    }

    private Expression BuildIsEmptyExpression(Expression property)
    {
        if (property.Type == typeof(string))
        {
            var isNull = Expression.Equal(property, Expression.Constant(null, typeof(string)));
            var isEmpty = Expression.Equal(property, Expression.Constant(string.Empty));
            return Expression.OrElse(isNull, isEmpty);
        }

        return Expression.Equal(property, Expression.Constant(null, property.Type));
    }

    private Expression BuildIsNotEmptyExpression(Expression property)
    {
        if (property.Type == typeof(string))
        {
            var isNotNull = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
            var isNotEmpty = Expression.NotEqual(property, Expression.Constant(string.Empty));
            return Expression.AndAlso(isNotNull, isNotEmpty);
        }

        return Expression.NotEqual(property, Expression.Constant(null, property.Type));
    }

    private Expression BuildGreaterThanExpression(Expression property, object? value)
    {
        var constantValue = Expression.Constant(ConvertValue(value, property.Type), property.Type);
        return Expression.GreaterThan(property, constantValue);
    }

    private Expression BuildLessThanExpression(Expression property, object? value)
    {
        var constantValue = Expression.Constant(ConvertValue(value, property.Type), property.Type);
        return Expression.LessThan(property, constantValue);
    }

    private Expression BuildGreaterThanOrEqualExpression(Expression property, object? value)
    {
        var constantValue = Expression.Constant(ConvertValue(value, property.Type), property.Type);
        return Expression.GreaterThanOrEqual(property, constantValue);
    }

    private Expression BuildLessThanOrEqualExpression(Expression property, object? value)
    {
        var constantValue = Expression.Constant(ConvertValue(value, property.Type), property.Type);
        return Expression.LessThanOrEqual(property, constantValue);
    }

    private Expression BuildInExpression(Expression property, object? value)
    {
        if (value is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            var list = new List<object>();
            foreach (var item in jsonElement.EnumerateArray())
            {
                list.Add(item.ToString());
            }

            value = list;
        }

        if (value is IEnumerable<object> enumerable)
        {
            var values = enumerable.Select(v => ConvertValue(v, property.Type)).ToList();
            var method = typeof(Enumerable).GetMethods()
                .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                .MakeGenericMethod(property.Type);

            var valuesConstant = Expression.Constant(values);
            return Expression.Call(method, valuesConstant, property);
        }

        return Expression.Constant(false);
    }

    private Expression BuildNotInExpression(Expression property, object? value)
    {
        return Expression.Not(BuildInExpression(property, value));
    }

    private object? ConvertValue(object? value, Type targetType)
    {
        if (value == null)
        {
            return null;
        }

        // Handle JsonElement
        if (value is System.Text.Json.JsonElement jsonElement)
        {
            value = jsonElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => jsonElement.GetString(),
                System.Text.Json.JsonValueKind.Number => jsonElement.GetDecimal(),
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                _ => jsonElement.ToString(),
            };

            if (value == null)
            {
                return null;
            }
        }

        try
        {
            if (targetType == typeof(string))
            {
                return value.ToString();
            }

            if (targetType == typeof(int) || targetType == typeof(int?))
            {
                return Convert.ToInt32(value);
            }

            if (targetType == typeof(DateTime) || targetType == typeof(DateTime?))
            {
                return Convert.ToDateTime(value);
            }

            if (targetType == typeof(decimal) || targetType == typeof(decimal?))
            {
                return Convert.ToDecimal(value);
            }

            if (targetType == typeof(bool) || targetType == typeof(bool?))
            {
                return Convert.ToBoolean(value);
            }

            return Convert.ChangeType(value, targetType);
        }
        catch (FormatException)
        {
            // Value format is incorrect for target type
            return value;
        }
        catch (InvalidCastException)
        {
            // Cannot cast value to target type
            return value;
        }
        catch (OverflowException)
        {
            // Value is too large or too small for target type
            return value;
        }
    }
}
