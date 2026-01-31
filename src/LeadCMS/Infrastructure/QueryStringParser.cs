// <copyright file="QueryStringParser.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.RegularExpressions;
using LeadCMS.Entities;

namespace LeadCMS.Infrastructure
{
    public class QueryStringParser
    {
        public static List<QueryCommand> Parse(string query)
        {
            // Strip leading '?' if present
            if (query.StartsWith('?'))
            {
                query = query.Substring(1);
            }

            var queryCommands = query.Length > 0 ? query.Split('&') : new string[0];

            var processedCommands = new List<QueryCommand>();
            var errorList = new List<QueryException>();

            foreach (var cmd in queryCommands)
            {
                var match = Regex.Match(cmd, "query+?=(?'value'.*)");
                if (match.Success)
                {
                    var qcmd = new QueryCommand()
                    {
                        Type = FilterType.Search,
                        Props = new string[0],
                        Value = match.Groups["value"].Captures[0].Value,
                        Source = cmd,
                    };
                    processedCommands.Add(qcmd);
                }
                else
                {
                    match = Regex.Match(cmd, "filter(\\[(?'property'.*?)\\])+?=(?'value'.*)");
                    if (!match.Success)
                    {
                        var parts = cmd.Split('=', 2);
                        if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]))
                        {
                            var paramName = parts[0].ToLowerInvariant();
                            var paramValue = parts[1];

                            // Check if this is a known filter type (limit, skip, order)
                            if (QueryCommand.FilterMappings.TryGetValue(paramName, out var filterType))
                            {
                                processedCommands.Add(new QueryCommand
                                {
                                    Type = filterType,
                                    Props = Array.Empty<string>(),
                                    Value = paramValue,
                                    Source = cmd,
                                    IsImplicit = true,
                                });
                            }
                            else
                            {
                                // Treat as implicit where filter
                                processedCommands.Add(new QueryCommand
                                {
                                    Type = FilterType.Where,
                                    Props = new[] { parts[0] },
                                    Value = paramValue,
                                    Source = cmd,
                                    IsImplicit = true,
                                });
                            }
                        }

                        continue;
                    }

                    var type = match.Groups["property"].Captures[0].Value.ToLowerInvariant();

                    if (string.IsNullOrEmpty(type) || string.IsNullOrWhiteSpace(type) || !QueryCommand.FilterMappings.ContainsKey(type))
                    {
                        errorList.Add(new QueryException(cmd, $"Failed to parse command. Operator '{type}' not found. Available operators: {QueryCommand.AvailableCommandString}"));
                        continue;
                    }

                    var qcmd = new QueryCommand()
                    {
                        Type = QueryCommand.FilterMappings.First(m => m.Key == type).Value,
                        Props = match.Groups["property"].Captures.Skip(1).Select(capture => capture.Value).ToArray(),
                        Value = match.Groups["value"].Captures[0].Value,
                        Source = cmd,
                    };
                    processedCommands.Add(qcmd);
                }
            }

            if (errorList.Any())
            {
                throw new QueryException(errorList);
            }

            return processedCommands;
        }
    }
}