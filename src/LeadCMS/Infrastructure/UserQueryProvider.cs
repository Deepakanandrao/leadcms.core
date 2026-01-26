// <copyright file="UserQueryProvider.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Linq.Expressions;
using System.Reflection;
using System.Web;
using LeadCMS.Configuration;
using LeadCMS.DataAnnotations;
using LeadCMS.Entities;
using LeadCMS.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LeadCMS.Infrastructure
{
    /// <summary>
    /// Query provider specifically for User entities.
    /// This reuses the QueryStringParser but handles string-based IDs.
    /// </summary>
    public class UserQueryProvider
    {
        private readonly IHttpContextHelper httpContextHelper;
        private readonly int maxListSize;

        public UserQueryProvider(IHttpContextHelper httpContextHelper, IOptions<ApiSettingsConfig> apiSettingsConfig)
        {
            this.httpContextHelper = httpContextHelper;
            maxListSize = apiSettingsConfig.Value.MaxListSize;
        }

        public async Task<UserQueryResult> GetResult(IQueryable<User> query, int limit = -1)
        {
            var queryString = httpContextHelper.Request.QueryString.HasValue
                ? HttpUtility.UrlDecode(httpContextHelper.Request.QueryString.ToString())
                : string.Empty;

            var queryCommands = QueryStringParser.Parse(queryString);

            var effectiveLimit = limit == -1 ? maxListSize : limit;

            // Parse query commands
            var idsFilter = ParseIdsFilter(queryCommands);
            var searchTerms = ParseSearchTerms(queryCommands);
            var orderData = ParseOrderCommands(queryCommands);
            var skipValue = ParseSkip(queryCommands);
            var limitValue = ParseLimit(queryCommands, effectiveLimit);
            var whereData = ParseWhereCommands(queryCommands);

            // Apply filters
            if (idsFilter.Count > 0)
            {
                query = query.Where(u => idsFilter.Contains(u.Id));
            }

            // Apply where clauses
            query = ApplyWhereCommands(query, whereData);

            // Apply search
            if (searchTerms.Count > 0)
            {
                query = ApplySearch(query, searchTerms);
            }

            // Count before paging
            var totalCount = await query.CountAsync();

            // Apply ordering
            query = ApplyOrdering(query, orderData);

            // Apply skip and limit
            if (skipValue > 0)
            {
                query = query.Skip(skipValue);
            }

            if (limitValue > 0)
            {
                query = query.Take(limitValue);
            }

            var records = await query.ToListAsync();

            return new UserQueryResult(records, totalCount);
        }

        private static List<string> ParseIdsFilter(List<QueryCommand> commands)
        {
            var cmd = commands.FirstOrDefault(c => c.Type == FilterType.Ids);
            if (cmd == null || string.IsNullOrWhiteSpace(cmd.Value))
            {
                return new List<string>();
            }

            return cmd.Value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private static List<string> ParseSearchTerms(List<QueryCommand> commands)
        {
            return commands
                .Where(c => c.Type == FilterType.Search && c.Value.Length > 0)
                .Select(c => c.Value)
                .ToList();
        }

        private static int ParseSkip(List<QueryCommand> commands)
        {
            var cmd = commands.FirstOrDefault(c => c.Type == FilterType.Skip);
            if (cmd != null && int.TryParse(cmd.Value, out var value) && value >= 0)
            {
                return value;
            }

            return 0;
        }

        private static int ParseLimit(List<QueryCommand> commands, int maxLimit)
        {
            var cmd = commands.FirstOrDefault(c => c.Type == FilterType.Limit);
            if (cmd != null && int.TryParse(cmd.Value, out var value) && value > 0)
            {
                return Math.Min(value, maxLimit);
            }

            return maxLimit;
        }

        private static List<UserOrderData> ParseOrderCommands(List<QueryCommand> commands)
        {
            var result = new List<UserOrderData>();
            var orderCommands = commands.Where(c => c.Type == FilterType.Order).ToArray();

            foreach (var cmd in orderCommands)
            {
                var propName = cmd.Props.ElementAtOrDefault(0);
                if (string.IsNullOrWhiteSpace(propName))
                {
                    propName = cmd.Value;
                }

                if (string.IsNullOrWhiteSpace(propName))
                {
                    continue;
                }

                // Parse direction from value (e.g., "displayName desc")
                var parts = propName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var actualPropName = parts[0];
                var ascending = true;

                if (parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase))
                {
                    ascending = false;
                }

                // Also check cmd.Value for direction
                if (cmd.Value.Equals("desc", StringComparison.OrdinalIgnoreCase))
                {
                    ascending = false;
                }

                var property = typeof(User).GetProperties()
                    .FirstOrDefault(p => p.Name.Equals(actualPropName, StringComparison.OrdinalIgnoreCase));

                if (property != null)
                {
                    result.Add(new UserOrderData(property, ascending));
                }
            }

            // Default to Id if no order specified
            if (result.Count == 0)
            {
                var idProperty = typeof(User).GetProperty("Id");
                if (idProperty != null)
                {
                    result.Add(new UserOrderData(idProperty, true));
                }
            }

            return result;
        }

        private static List<UserWhereData> ParseWhereCommands(List<QueryCommand> commands)
        {
            var result = new List<UserWhereData>();

            foreach (var cmd in commands.Where(c => c.Type == FilterType.Where))
            {
                // Props should be [propertyName, operator] e.g., ["email", "eq"]
                if (cmd.Props.Length < 2)
                {
                    continue;
                }

                var propertyName = cmd.Props[0];
                var operatorName = cmd.Props[1].ToLowerInvariant();

                var property = typeof(User).GetProperties()
                    .FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));

                if (property != null)
                {
                    result.Add(new UserWhereData(property, operatorName, cmd.Value));
                }
            }

            return result;
        }

        private static IQueryable<User> ApplySearch(IQueryable<User> query, List<string> searchTerms)
        {
            var searchableProps = typeof(User).GetProperties()
                .Where(p => p.IsDefined(typeof(SearchableAttribute), false))
                .ToList();

            // Also search in Email and UserName which are common search fields
            var emailProp = typeof(User).GetProperty("Email");
            var userNameProp = typeof(User).GetProperty("UserName");

            if (emailProp != null && !searchableProps.Contains(emailProp))
            {
                searchableProps.Add(emailProp);
            }

            if (userNameProp != null && !searchableProps.Contains(userNameProp))
            {
                searchableProps.Add(userNameProp);
            }

            if (searchableProps.Count == 0)
            {
                return query;
            }

            foreach (var term in searchTerms)
            {
                var lowerTerm = term.ToLowerInvariant();
                var paramExpr = Expression.Parameter(typeof(User), "u");

                Expression? combinedOrExpression = null;

                foreach (var prop in searchableProps)
                {
                    if (prop.PropertyType != typeof(string))
                    {
                        continue;
                    }

                    // Build: u.Property != null && u.Property.ToLower().Contains(term)
                    var propExpr = Expression.Property(paramExpr, prop);
                    var nullCheck = Expression.NotEqual(propExpr, Expression.Constant(null, typeof(string)));
                    var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes)!;
                    var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) })!;
                    var toLowerExpr = Expression.Call(propExpr, toLowerMethod);
                    var containsExpr = Expression.Call(toLowerExpr, containsMethod, Expression.Constant(lowerTerm));
                    var andExpr = Expression.AndAlso(nullCheck, containsExpr);

                    combinedOrExpression = combinedOrExpression == null
                        ? andExpr
                        : Expression.OrElse(combinedOrExpression, andExpr);
                }

                if (combinedOrExpression != null)
                {
                    var lambda = Expression.Lambda<Func<User, bool>>(combinedOrExpression, paramExpr);
                    query = query.Where(lambda);
                }
            }

            return query;
        }

        private static IQueryable<User> ApplyOrdering(IQueryable<User> query, List<UserOrderData> orderData)
        {
            bool isFirst = true;

            foreach (var order in orderData)
            {
                var paramExpr = Expression.Parameter(typeof(User), "u");
                var propExpr = Expression.Property(paramExpr, order.Property);
                var lambda = Expression.Lambda(propExpr, paramExpr);

                string methodName;
                if (order.Ascending)
                {
                    methodName = isFirst ? "OrderBy" : "ThenBy";
                }
                else
                {
                    methodName = isFirst ? "OrderByDescending" : "ThenByDescending";
                }

                var method = typeof(Queryable).GetMethods()
                    .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                    .MakeGenericMethod(typeof(User), order.Property.PropertyType);

                query = (IQueryable<User>)method.Invoke(null, new object[] { query, lambda })!;
                isFirst = false;
            }

            return query;
        }

        private static IQueryable<User> ApplyWhereCommands(IQueryable<User> query, List<UserWhereData> whereData)
        {
            foreach (var where in whereData)
            {
                var paramExpr = Expression.Parameter(typeof(User), "u");
                var propExpr = Expression.Property(paramExpr, where.Property);

                Expression? comparison = null;

                if (where.Property.PropertyType == typeof(string))
                {
                    var valueExpr = Expression.Constant(where.Value);

                    comparison = where.Operator switch
                    {
                        "eq" => Expression.Equal(propExpr, valueExpr),
                        "ne" => Expression.NotEqual(propExpr, valueExpr),
                        "contains" => Expression.Call(propExpr, typeof(string).GetMethod("Contains", new[] { typeof(string) })!, valueExpr),
                        "startswith" => Expression.Call(propExpr, typeof(string).GetMethod("StartsWith", new[] { typeof(string) })!, valueExpr),
                        "endswith" => Expression.Call(propExpr, typeof(string).GetMethod("EndsWith", new[] { typeof(string) })!, valueExpr),
                        _ => null,
                    };
                }
                else if (where.Property.PropertyType == typeof(DateTime) || where.Property.PropertyType == typeof(DateTime?))
                {
                    if (DateTime.TryParse(where.Value, out var dateValue))
                    {
                        var valueExpr = Expression.Constant(dateValue, where.Property.PropertyType);

                        comparison = where.Operator switch
                        {
                            "eq" => Expression.Equal(propExpr, valueExpr),
                            "ne" => Expression.NotEqual(propExpr, valueExpr),
                            "gt" => Expression.GreaterThan(propExpr, valueExpr),
                            "gte" => Expression.GreaterThanOrEqual(propExpr, valueExpr),
                            "lt" => Expression.LessThan(propExpr, valueExpr),
                            "lte" => Expression.LessThanOrEqual(propExpr, valueExpr),
                            _ => null,
                        };
                    }
                }
                else if ((where.Property.PropertyType == typeof(bool) || where.Property.PropertyType == typeof(bool?)) && bool.TryParse(where.Value, out var boolValue))
                {
                    var valueExpr = Expression.Constant(boolValue, where.Property.PropertyType);

                    comparison = where.Operator switch
                    {
                        "eq" => Expression.Equal(propExpr, valueExpr),
                        "ne" => Expression.NotEqual(propExpr, valueExpr),
                        _ => null,
                    };
                }

                if (comparison != null)
                {
                    var lambda = Expression.Lambda<Func<User, bool>>(comparison, paramExpr);
                    query = query.Where(lambda);
                }
            }

            return query;
        }

        private sealed class UserOrderData
        {
            public UserOrderData(PropertyInfo property, bool ascending)
            {
                Property = property;
                Ascending = ascending;
            }

            public PropertyInfo Property { get; }

            public bool Ascending { get; }
        }

        private sealed class UserWhereData
        {
            public UserWhereData(PropertyInfo property, string op, string value)
            {
                Property = property;
                Operator = op;
                Value = value;
            }

            public PropertyInfo Property { get; }

            public string Operator { get; }

            public string Value { get; }
        }
    }
}
