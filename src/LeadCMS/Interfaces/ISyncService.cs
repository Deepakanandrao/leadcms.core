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
    /// <returns>ActionResult containing sync response with items, deleted IDs, and headers.</returns>
    Task<IActionResult> SyncAsync<TEntity, TDto>(
        QueryProviderFactory<TEntity> queryProviderFactory,
        IMapper mapper,
        string? syncToken = null,
        string? query = null)
        where TEntity : BaseEntityWithId, new()
        where TDto : class;
}