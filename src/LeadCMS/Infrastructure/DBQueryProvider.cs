// <copyright file="DBQueryProvider.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using LeadCMS.DataAnnotations;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace LeadCMS.Infrastructure
{
    public class DBQueryProvider<T> : IQueryProvider<T>
        where T : BaseEntityWithId
    {
        private readonly QueryModelBuilder<T> queryBuilder;
        
        public DBQueryProvider(IQueryable<T> query, QueryModelBuilder<T> queryBuilder)
        {
            BuiltQuery = query;
            this.queryBuilder = queryBuilder;
        }        

        public IQueryable<T> BuiltQuery { get; private set; }

        public Array? DynamicResults { get; private set; }

        public async Task<QueryResult<T>> GetResult()
        {
            if (queryBuilder.Ids != null && queryBuilder.Ids.Count > 0)
            {
                BuiltQuery = BuiltQuery.Where(e => queryBuilder.Ids.Contains(e.Id));
            }
            
            AddWhereCommands();
            AddSearchCommands();

            var totalCount = BuiltQuery.Count();
            IList<T>? records;

            AddIncludeCommands();
            AddOrderCommands();
            AddSkipCommand();
            AddLimitCommand();
            if (queryBuilder.SelectData.IsSelect)
            {
                records = await GetSelectResult();
                var result = new QueryResult<T>(records, totalCount);
                result.DynamicResults = DynamicResults;
                return result;
            }
            else
            {
                records = await BuiltQuery.ToListAsync();
            }

            return new QueryResult<T>(records, totalCount, "DB");
        }

        private static bool CanTranslateToString(Type propertyType)
        {
            // Only allow types that Entity Framework can successfully translate ToString() calls for
            var underlyingType = Nullable.GetUnderlyingType(propertyType);
            var typeToCheck = underlyingType ?? propertyType;
            
            // Allowed types that EF can translate
            return typeToCheck == typeof(int) ||
                   typeToCheck == typeof(long) ||
                   typeToCheck == typeof(short) ||
                   typeToCheck == typeof(byte) ||
                   typeToCheck == typeof(sbyte) ||
                   typeToCheck == typeof(uint) ||
                   typeToCheck == typeof(ulong) ||
                   typeToCheck == typeof(ushort) ||
                   typeToCheck == typeof(float) ||
                   typeToCheck == typeof(double) ||
                   typeToCheck == typeof(decimal) ||
                   typeToCheck == typeof(bool) ||
                   typeToCheck == typeof(char) ||
                   typeToCheck == typeof(Guid);
        }

        private void AddIncludeCommands()
        {
            foreach (var data in queryBuilder.IncludeData)
            {
                BuiltQuery = BuiltQuery.Include(data.Name);
            }
        }

        private void AddOrderCommands()
        {
            if (queryBuilder.OrderData.Count == 0)
            {
                BuiltQuery = BuiltQuery.OrderBy(t => t.Id);
            }
            else
            {
                var moreThanOne = false;
                foreach (var orderCmd in queryBuilder.OrderData)
                {
                    var expressionParameter = Expression.Parameter(typeof(T));
                    var orderPropertyType = orderCmd.Property.PropertyType;
                    var orderPropertyExpression = Expression.Property(expressionParameter, orderCmd.Property.Name);
                    var orderDelegateType = typeof(Func<,>).MakeGenericType(typeof(T), orderPropertyType);
                    var orderLambda = Expression.Lambda(orderDelegateType, orderPropertyExpression, expressionParameter);
                    var methodName = string.Empty;

                    if (orderCmd.Ascending)
                    {
                        methodName = moreThanOne ? "ThenBy" : "OrderBy";
                    }
                    else
                    {
                        methodName = moreThanOne ? "ThenByDescending" : "OrderByDescending";
                    }

                    moreThanOne = true;

                    var orderMethod = typeof(Queryable).GetMethods().First(
                                                                        m => m.Name == methodName &&
                                                                        m.GetGenericArguments().Length == 2 &&
                                                                        m.GetParameters().Length == 2).MakeGenericMethod(typeof(T), orderPropertyType);
                    BuiltQuery = (IOrderedQueryable<T>)orderMethod.Invoke(null, new object?[] { BuiltQuery, orderLambda })!;
                }
            }
        }

        private void AddSkipCommand()
        {
            if (queryBuilder.Skip > 0)
            {
                BuiltQuery = BuiltQuery.Skip(queryBuilder.Skip);
            }
        }

        private void AddLimitCommand()
        {
            if (queryBuilder.Limit > 0)
            {
                BuiltQuery = BuiltQuery.Take(queryBuilder.Limit);
            }
        }

        private void AddSearchCommands()
        {
            foreach (var cmdValue in queryBuilder.SearchData)
            {
                var props = typeof(T).GetProperties().Where(p => p.IsDefined(typeof(SearchableAttribute), false));

                Expression orExpression = Expression.Constant(false);
                var paramExpr = Expression.Parameter(typeof(T), "entity");
                var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });

                foreach (var prop in props)
                {
                    if (prop != null)
                    {
                        var n = prop.Name;
                        var me = Expression.Property(paramExpr, n);
                        Expression containsExpression;
                        
                        if (prop.PropertyType == typeof(string))
                        {
                            containsExpression = Expression.Call(me, containsMethod!, Expression.Constant(cmdValue));
                        }
                        else if (prop.PropertyType == typeof(string[]))
                        {
                            // For string arrays, use EF.Functions.JsonContains or similar array contains method
                            // Skip array properties for now as they require special handling
                            continue;
                        }
                        else if (prop.PropertyType.IsArray)
                        {
                            // Skip other array types that can't be easily converted to string
                            continue;
                        }
                        else
                        {
                            // Skip types that Entity Framework cannot translate to SQL
                            var underlyingType = Nullable.GetUnderlyingType(prop.PropertyType);
                            var typeToCheck = underlyingType ?? prop.PropertyType;
                            
                            // Skip enums (including nullable enums) as EF cannot translate enum.ToString()
                            if (typeToCheck.IsEnum)
                            {
                                continue;
                            }
                            
                            // Skip complex types like Dictionary, custom classes, etc.
                            if (!typeToCheck.IsPrimitive && typeToCheck != typeof(DateTime) && typeToCheck != typeof(decimal) && typeToCheck != typeof(Guid))
                            {
                                continue;
                            }
                            
                            // For supported primitive types and DateTime, convert to string
                            if (CanTranslateToString(prop.PropertyType))
                            {
                                var toStringMethod = prop.PropertyType.GetMethod("ToString", new Type[0]);
                                if (toStringMethod != null)
                                {
                                    var ce = Expression.Call(me, toStringMethod);
                                    containsExpression = Expression.Call(ce, containsMethod!, Expression.Constant(cmdValue));
                                }
                                else
                                {
                                    continue;
                                }
                            }
                            else
                            {
                                continue;
                            }
                        }

                        orExpression = Expression.Or(orExpression, containsExpression);
                    }
                }

                if (!ExpressionEqualityComparer.Instance.Equals(orExpression, Expression.Constant(false)))
                {
                    var predicate = Expression.Lambda<Func<T, bool>>(orExpression, paramExpr);
                    BuiltQuery = BuiltQuery.Where(predicate);
                }
            }
        }

        private void AddWhereCommands()
        {
            var commands = queryBuilder.WhereData;
            if (commands.Count > 0)
            {
                var expressionParameter = Expression.Parameter(typeof(T));
                Expression andExpression = Expression.Constant(true);
                var andExpressionExist = false;
                Expression orExpression = Expression.Constant(false);
                var errorList = new List<QueryException>();

                foreach (var cmds in commands)
                {
                    try
                    {
                        if (cmds.OrOperation)
                        {
                            foreach (var cmd in cmds.Data)
                            {
                                var expression = ParseWhereCommand(expressionParameter, cmd);
                                orExpression = Expression.Or(orExpression, expression);
                            }
                        }
                        else
                        {
                            foreach (var cmd in cmds.Data)
                            {
                                var expression = ParseWhereCommand(expressionParameter, cmd);
                                andExpression = Expression.And(andExpression, expression);
                                andExpressionExist = true;
                            }
                        }
                    }
                    catch (QueryException e)
                    {
                        errorList.Add(e);
                    }
                }

                if (errorList.Any())
                {
                    throw new QueryException(errorList);
                }

                if (!andExpressionExist)
                {
                    andExpression = Expression.Constant(false);
                }

                var resExpression = Expression.Or(andExpression, orExpression);
                BuiltQuery = BuiltQuery.Where(Expression.Lambda<Func<T, bool>>(resExpression, expressionParameter));
            }
        }

        private Expression ParseWhereCommand(ParameterExpression expressionParameter, QueryModelBuilder<T>.WhereUnitData cmd)
        {
            Expression outputExpression;
            var parameterPropertyExpression = Expression.Property(expressionParameter, cmd.Property.Name);

            Expression CreateEqualExpression(QueryModelBuilder<T>.WhereUnitData cmd, Expression parameter)
            {
                Expression orExpression = Expression.Constant(false);
                var stringValues = cmd.ParseStringValues();
                var parsedValues = cmd.ParseValues(stringValues);

                foreach (var value in parsedValues)
                {
                    if (value == null && !cmd.IsNullableProperty())
                    {
                        return Expression.Constant(false);
                    }
                    else
                    {
                        var valueParameterExpression = Expression.Constant(value, cmd.Property.PropertyType);
                        var eqExpression = Expression.Equal(parameter, valueParameterExpression);
                        orExpression = Expression.Or(orExpression, eqExpression);
                    }
                }

                return orExpression;
            }

            Expression CreateNEqualExpression(QueryModelBuilder<T>.WhereUnitData cmd, Expression parameter)
            {
                var expression = CreateEqualExpression(cmd, parameter);
                return Expression.Not(expression);
            }

            Expression? CreateCompareExpression(QueryModelBuilder<T>.WhereUnitData cmd, Expression parameter)
            {
                Expression? res = null;
                var parsedValue = cmd.ParseValues(new string[] { cmd.StringValue })[0];

                Expression value = Expression.Constant(parsedValue, cmd.Property.PropertyType);
                var pEx = parameter;
                var vEx = value;

                if (cmd.Property.PropertyType == typeof(string))
                {
                    var compareMethod = cmd.Property.PropertyType.GetMethod("CompareTo", new[] { typeof(string) });
                    pEx = Expression.Call(parameter, compareMethod!, value);
                    vEx = Expression.Constant(0);
                }

                if (cmd.Operation == WOperand.GreaterThan)
                {
                    res = Expression.GreaterThan(pEx, vEx);
                }
                else if (cmd.Operation == WOperand.GreaterThanOrEqualTo)
                {
                    res = Expression.GreaterThanOrEqual(pEx, vEx);
                }
                else if (cmd.Operation == WOperand.LessThan)
                {
                    res = Expression.LessThan(pEx, vEx);
                }
                else if (cmd.Operation == WOperand.LessThanOrEqualTo)
                {
                    res = Expression.LessThanOrEqual(pEx, vEx);
                }

                return res;
            }

            Expression? CreateLikeExpression(QueryModelBuilder<T>.WhereUnitData cmd, Expression parameter)
            {
                var parsedValue = cmd.ParseValues(new string[] { cmd.StringValue })[0];

                Expression value = Expression.Constant(parsedValue, cmd.Property.PropertyType);
                Expression? res = null;

                var matchOperation = typeof(Regex).GetMethod("IsMatch", BindingFlags.Static | BindingFlags.Public, new[] { typeof(string), typeof(string), typeof(RegexOptions) });
                var trueConstant = Expression.Constant(true);
                var falseConstant = Expression.Constant(false);
                var regexOptionExpression = Expression.Constant(RegexOptions.Compiled);

                if (cmd.Operation == WOperand.Like)
                {
                    res = Expression.Equal(Expression.Call(matchOperation!, parameter, value, regexOptionExpression), trueConstant);
                }
                else if (cmd.Operation == WOperand.NLike)
                {
                    res = Expression.Equal(Expression.Call(matchOperation!, parameter, value, regexOptionExpression), falseConstant);
                }

                return res;
            }

            Expression? CreateContainExpression(QueryModelBuilder<T>.WhereUnitData cmd, Expression parameter)
            {
                Expression? res = null;

                var matchOperation = typeof(Regex).GetMethod("IsMatch", BindingFlags.Static | BindingFlags.Public, new[] { typeof(string), typeof(string), typeof(RegexOptions) });
                var trueConstant = Expression.Constant(true);
                var falseConstant = Expression.Constant(false);
                var regexOptionExpression = Expression.Constant(RegexOptions.Compiled);

                var data = cmd.ParseContainValue(cmd.StringValue);
                var sb = new StringBuilder();

                sb.Append('^');
                foreach (var d in data)
                {
                    if (d.Item1 == QueryModelBuilder<T>.WhereUnitData.ContainsType.MatchAll)
                    {
                        sb.Append("(.*)");
                    }
                    else if (d.Item1 == QueryModelBuilder<T>.WhereUnitData.ContainsType.Substring)
                    {
                        sb.Append(Regex.Escape(d.Item2));
                    }
                }

                sb.Append('$');

                var valueParameterExpression = Expression.Constant(sb.ToString(), typeof(string));

                if (cmd.Operation == WOperand.Contains)
                {
                    res = Expression.Equal(Expression.Call(matchOperation!, parameter, valueParameterExpression, regexOptionExpression), trueConstant);
                }
                else if (cmd.Operation == WOperand.NContains)
                {
                    res = Expression.Equal(Expression.Call(matchOperation!, parameter, valueParameterExpression, regexOptionExpression), falseConstant);
                }

                return res;
            }

            try
            {
                switch (cmd.Operation)
                {
                    case WOperand.Equal:
                        outputExpression = CreateEqualExpression(cmd, parameterPropertyExpression);
                        break;
                    case WOperand.NotEqual:
                        outputExpression = CreateNEqualExpression(cmd, parameterPropertyExpression);
                        break;
                    case WOperand.InList:
                        outputExpression = CreateInListExpression(cmd, parameterPropertyExpression);
                        break;
                    case WOperand.GreaterThan:
                    case WOperand.GreaterThanOrEqualTo:
                    case WOperand.LessThan:
                    case WOperand.LessThanOrEqualTo:
                        outputExpression = CreateCompareExpression(cmd, parameterPropertyExpression)!;
                        break;
                    case WOperand.Like:
                    case WOperand.NLike:
                        outputExpression = CreateLikeExpression(cmd, parameterPropertyExpression)!;
                        break;
                    case WOperand.Contains:
                    case WOperand.NContains:
                        outputExpression = CreateContainExpression(cmd, parameterPropertyExpression)!;
                        break;
                    default:
                        throw new QueryException(cmd.Cmd.Source, $"No such operand '{cmd.Operation}'");
                }
            }
            catch (Exception ex)
            {
                throw new QueryException(cmd.Cmd.Source, ex.Message);
            }

            return outputExpression;
        }

        private Expression CreateInListExpression(QueryModelBuilder<T>.WhereUnitData cmd, Expression parameter)
        {
            // Parse comma-separated values, trim whitespace, and convert to property type
            var stringValues = cmd.StringValue.Split(',').Select(s => s.Trim()).ToArray();
            var parsedValues = cmd.ParseValues(stringValues);
            var valuesArray = Array.CreateInstance(cmd.Property.PropertyType, parsedValues.Count);
            for (int i = 0; i < parsedValues.Count; i++)
            {
                valuesArray.SetValue(parsedValues[i], i);
            }

            var containsMethod = typeof(Enumerable).GetMethods()
                .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                .MakeGenericMethod(cmd.Property.PropertyType);
            var arrayExpr = Expression.Constant(valuesArray);
            return Expression.Call(containsMethod, arrayExpr, parameter);
        }

        private async Task<IList<T>?> GetSelectResult()
        {
            if (queryBuilder.SelectData.SelectedProperties.Any())
            {
                var expressionParameter = Expression.Parameter(typeof(T));
                var outputType = TypeHelper.CompileTypeForSelectStatement(queryBuilder.SelectData.SelectedProperties.ToArray());
                var delegateType = typeof(Func<,>).MakeGenericType(typeof(T), outputType);
                var createOutputTypeExpression = Expression.New(outputType);

                var expressionSelectedProperties = queryBuilder.SelectData.SelectedProperties.Select(p =>
                {
                    var bindProp = outputType.GetProperty(p.Name);
                    var exprProp = Expression.Property(expressionParameter, p);
                    return Expression.Bind(bindProp!, exprProp);
                }).ToArray();
                var expressionCreateArray = Expression.MemberInit(createOutputTypeExpression, expressionSelectedProperties);
                dynamic lambda = Expression.Lambda(delegateType, expressionCreateArray, expressionParameter);

                var queryMethod = typeof(Queryable).GetMethods().FirstOrDefault(m => m.Name == "Select" && m.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments().Length == 2)!.MakeGenericMethod(typeof(T), outputType);

                var toArrayAsyncMethod = typeof(EntityFrameworkQueryableExtensions).GetMethod("ToArrayAsync")!.MakeGenericMethod(outputType);

                var selectQueryable = queryMethod!.Invoke(BuiltQuery, new object[] { BuiltQuery, lambda });

                var outputTypeTaskResultProp = typeof(Task<>).MakeGenericType(outputType.MakeArrayType()).GetProperty("Result");

                var selectResult = (Task)toArrayAsyncMethod.Invoke(selectQueryable, new object?[] { selectQueryable!, null })!;
                await selectResult;
                var taskResult = outputTypeTaskResultProp!.GetValue(selectResult);
                if (taskResult is Array arr)
                {
                    DynamicResults = arr;
                }

                return taskResult as IList<T>;
            }
            else
            {
                return null;
            }
        }        
    }
}