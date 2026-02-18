// <copyright file="ISyncService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Data;
using LeadCMS.Entities;
using LeadCMS.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace LeadCMS.Interfaces;

/// <summary>
/// Service for handling synchronization operations across different entity types.
/// Provides reusable sync functionality that can be used by controllers that don't inherit from BaseController.
/// </summary>
public interface ISyncService
{
    /// <summary>
    /// Performs synchronization for the specified entity type.
    /// Returns changed entities and deleted entity IDs since the last sync token.
    /// </summary>
    /// <typeparam name="TEntity">The entity type that must inherit from BaseEntityWithId and implement timestamp interfaces.</typeparam>
    /// <typeparam name="TDto">The DTO type to map entities to.</typeparam>
    /// <param name="queryProviderFactory">Factory for building queries with optional filtering.</param>
    /// <param name="mapper">AutoMapper instance for entity to DTO mapping.</param>
    /// <param name="syncToken">Optional sync token indicating the last sync time.</param>
    /// <param name="query">Optional query string for additional filtering.</param>
    /// <param name="includeBase">When true, includes base versions of modified entities (the version at sync token time) for three-way merge support.</param>
    /// <returns>ActionResult containing sync response with items, deleted IDs, and headers.</returns>
    Task<IActionResult> SyncAsync<TEntity, TDto>(
        QueryProviderFactory<TEntity> queryProviderFactory,
        IMapper mapper,
        string? syncToken = null,
        string? query = null,
        bool includeBase = false)
        where TEntity : BaseEntityWithId, new()
        where TDto : class;

    /// <summary>
    /// Performs synchronization specifically for media files.
    /// Returns changed media entities and deleted/renamed-away file paths (scopeUid + name)
    /// since the last sync token. Deleted paths are extracted from ChangeLog entries:
    /// Deleted entries provide the path of actually deleted files, and Modified entries
    /// provide the old path of renamed files.
    /// </summary>
    /// <param name="queryProviderFactory">Factory for building media queries with optional filtering.</param>
    /// <param name="mapper">AutoMapper instance for entity to DTO mapping.</param>
    /// <param name="syncToken">Optional sync token indicating the last sync time.</param>
    /// <param name="query">Optional query string for additional filtering.</param>
    /// <returns>ActionResult containing sync response with items and deleted file paths.</returns>
    Task<IActionResult> SyncMediaAsync(
        QueryProviderFactory<Media> queryProviderFactory,
        IMapper mapper,
        string? syncToken = null,
        string? query = null);
}