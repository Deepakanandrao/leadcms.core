// <copyright file="ChangeLogService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.Json;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Services;

/// <summary>
/// Service for managing ChangeLog records.
/// </summary>
public class ChangeLogService : IChangeLogService
{
    private readonly ILogger<ChangeLogService> logger;
    private readonly UserManager<User> userManager;

    public ChangeLogService(ILogger<ChangeLogService> logger, UserManager<User> userManager)
    {
        this.logger = logger;
        this.userManager = userManager;
    }

    /// <inheritdoc/>
    public TUpdateDto? SafeParseData<TUpdateDto>(string jsonData)
        where TUpdateDto : class
    {
        if (string.IsNullOrEmpty(jsonData))
        {
            return null;
        }

        try
        {
            return JsonHelper.Deserialize<TUpdateDto>(jsonData);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse ChangeLog JSON data into {UpdateDtoType}. JSON: {JsonData}", typeof(TUpdateDto).Name, jsonData);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error parsing ChangeLog JSON data into {UpdateDtoType}. JSON: {JsonData}", typeof(TUpdateDto).Name, jsonData);
            return null;
        }
    }

    /// <inheritdoc/>
    public string? ExtractCreatedById(string jsonData)
    {
        return ExtractFieldFromJson(jsonData, "createdById");
    }

    /// <inheritdoc/>
    public string? ExtractUpdatedById(string jsonData)
    {
        return ExtractFieldFromJson(jsonData, "updatedById");
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, string>> BatchResolveUserDisplayNamesAsync(IEnumerable<string?> userIds)
    {
        var result = new Dictionary<string, string>();

        // Filter out null/empty user IDs and get distinct values
        var validUserIds = userIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();

        if (!validUserIds.Any())
        {
            return result;
        }

        try
        {
            // Query all users at once using Entity Framework async methods
            var userList = await userManager.Users
                .Where(u => validUserIds.Contains(u.Id))
                .Select(u => new { u.Id, u.DisplayName })
                .ToListAsync();

            var users = userList.ToDictionary(u => u.Id, u => u.DisplayName ?? string.Empty);

            return users;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to batch resolve user display names for {UserCount} users", validUserIds.Count);
            return result;
        }
    }

    /// <summary>
    /// Extracts a specific field from JSON data safely.
    /// </summary>
    /// <param name="jsonData">The JSON data to extract from.</param>
    /// <param name="fieldName">The field name to extract.</param>
    /// <returns>The field value as string or null if not found.</returns>
    private string? ExtractFieldFromJson(string jsonData, string fieldName)
    {
        if (string.IsNullOrEmpty(jsonData))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(jsonData);
            if (document.RootElement.TryGetProperty(fieldName, out var property))
            {
                return property.ValueKind == JsonValueKind.Null ? null : property.GetString();
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to extract {FieldName} from ChangeLog JSON data. JSON: {JsonData}", fieldName, jsonData);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error extracting {FieldName} from ChangeLog JSON data. JSON: {JsonData}", fieldName, jsonData);
        }

        return null;
    }
}