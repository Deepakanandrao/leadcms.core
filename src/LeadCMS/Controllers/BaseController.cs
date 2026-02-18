// <copyright file="BaseController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Data;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Infrastructure;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Controllers
{
    public class BaseController<T, TC, TU, TD> : ControllerBase
        where T : BaseEntityWithId, new()
        where TC : class
        where TU : class
        where TD : class
    {
        protected readonly DbSet<T> dbSet;
        protected readonly PgDbContext dbContext;
        protected readonly IMapper mapper;
        protected readonly QueryProviderFactory<T> queryProviderFactory;
        protected readonly ISyncService syncService;

        public BaseController(PgDbContext dbContext, IMapper mapper, EsDbContext esDbContext, QueryProviderFactory<T> queryProviderFactory, ISyncService syncService)
        {
            this.dbContext = dbContext;
            this.mapper = mapper;

            dbSet = dbContext.Set<T>();
            this.queryProviderFactory = queryProviderFactory;
            this.syncService = syncService;
        }

        // GET api/{entity}s/5
        [HttpGet("{id}")]
        // [EnableQuery]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public virtual async Task<ActionResult<TD>> GetOne(int id)
        {
            var qp = queryProviderFactory.BuildQueryProvider(limit: 1, additionalQueryString: $"filter[where][id]={id}");
            var result = await qp.GetResult();

            if (result.Records == null || result.Records.Count == 0)
            {
                throw new EntityNotFoundException(typeof(T).Name, id.ToString());
            }

            var resultConverted = mapper.Map<TD>(result.Records.First());
            DtoCleanupHelper.RemoveSecondLevelObjects(new List<TD> { resultConverted });

            return Ok(resultConverted);
        }

        // POST api/{entity}s
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public virtual async Task<ActionResult<TD>> Post([FromBody] TC value)
        {
            var newValue = mapper.Map<T>(value);
            var result = await dbSet.AddAsync(newValue);
            await dbContext.SaveChangesAsync();

            await OnAfterCreateAsync(newValue);

            var resultsToClient = mapper.Map<TD>(newValue);

            return CreatedAtAction(nameof(GetOne), new { id = result.Entity.Id }, resultsToClient);
        }

        // PUT api/{entity}s/5
        [HttpPatch("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public virtual async Task<ActionResult<TD>> Patch(int id, [FromBody] TU value)
        {
            var existingEntity = await FindOrThrowNotFound(id);
            return await Patch(existingEntity, value);
        }

        // DELETE api/{entity}s/5
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public virtual async Task<ActionResult> Delete(int id)
        {
            var existingEntity = await FindOrThrowNotFound(id);

            dbContext.Remove(existingEntity);

            await dbContext.SaveChangesAsync();

            await OnAfterDeleteAsync(existingEntity);

            return NoContent();
        }

        // DELETE api/{entity}s/bulk
        [HttpDelete("bulk")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public virtual async Task<ActionResult> DeleteMany([FromBody] List<int> ids)
        {
            return await BulkDeleteHelper.ExecuteAsync(
                dbContext,
                dbSet,
                ids,
                onAfterDelete: async entities =>
                {
                    foreach (var entity in entities)
                    {
                        await OnAfterDeleteAsync(entity);
                    }
                });
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public virtual async Task<ActionResult<List<TD>>> Get([FromQuery] string? query)
        {
            var qp = queryProviderFactory.BuildQueryProvider();

            var result = await qp.GetResult();
            Response.Headers.Append(ResponseHeaderNames.TotalCount, result.TotalCount.ToString());
            Response.Headers.Append(ResponseHeaderNames.AccessControlExposeHeader, ResponseHeaderNames.TotalCount);
            if (!string.IsNullOrEmpty(result.ServedFrom))
            {
                Response.Headers.Append(ResponseHeaderNames.ServedFrom, result.ServedFrom);
            }

            var res = mapper.Map<List<TD>>(result.Records);
            DtoCleanupHelper.RemoveSecondLevelObjects(res);
            return Ok(res);
        }

        [HttpGet("export")]
        [Produces("text/csv", "text/json")]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public virtual async Task<ActionResult<List<TD>>> Export([FromQuery] string? query)
        {
            var qp = queryProviderFactory.BuildQueryProvider(int.MaxValue);

            var result = await qp.GetResult();
            Response.Headers.Append(ResponseHeaderNames.TotalCount, result.TotalCount.ToString());
            Response.Headers.Append(ResponseHeaderNames.AccessControlExposeHeader, ResponseHeaderNames.TotalCount);

            if (result.DynamicResults != null)
            {
                var dynamicList = new List<object>();
                foreach (var item in result.DynamicResults)
                {
                    dynamicList.Add(item);
                }

                return Ok(dynamicList);
            }

            if (!string.IsNullOrEmpty(result.ServedFrom))
            {
                Response.Headers.Append(ResponseHeaderNames.ServedFrom, result.ServedFrom);
            }

            var res = mapper.Map<List<TD>>(result.Records);
            DtoCleanupHelper.RemoveSecondLevelObjects(res);
            return Ok(res);
        }

        /// <summary>
        /// Synchronizes entity data based on a sync token for incremental updates.
        /// Returns a <c>SyncResponseDto&lt;TD, int&gt;</c> containing changed items and deleted entity IDs.
        /// Derived controllers should override and add a concrete <c>ProducesResponseType</c> attribute.
        /// </summary>
        [HttpGet("sync")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public virtual async Task<IActionResult> Sync([FromQuery] string? syncToken = null, [FromQuery] string? query = null)
        {
            return await syncService.SyncAsync<T, TD>(queryProviderFactory, mapper, syncToken, query);
        }

        protected async Task<T> FindOrThrowNotFound(int id)
        {
            var existingEntity = await (from p in dbSet
                                        where p.Id == id
                                        select p).FirstOrDefaultAsync();

            if (existingEntity == null)
            {
                throw new EntityNotFoundException(typeof(T).Name, id.ToString());
            }

            return existingEntity;
        }

        protected async Task<ActionResult<TD>> Patch(T existingEntity, TU value)
        {
            // AutoMapper automatically applies null properties if value implements IPatchDto
            mapper.Map(value, existingEntity);
            await dbContext.SaveChangesAsync();

            await OnAfterUpdateAsync(existingEntity);

            var resultsToClient = mapper.Map<TD>(existingEntity);

            return Ok(resultsToClient);
        }

        /// <summary>
        /// Called after an entity is successfully created. Override this method to add custom post-creation logic.
        /// </summary>
        /// <param name="entity">The created entity.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected virtual async Task OnAfterCreateAsync(T entity)
        {
            // Default implementation does nothing
            await Task.CompletedTask;
        }

        /// <summary>
        /// Called after an entity is successfully updated. Override this method to add custom post-update logic.
        /// </summary>
        /// <param name="entity">The updated entity.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected virtual async Task OnAfterUpdateAsync(T entity)
        {
            // Default implementation does nothing
            await Task.CompletedTask;
        }

        /// <summary>
        /// Called after an entity is successfully deleted. Override this method to add custom post-deletion logic.
        /// </summary>
        /// <param name="entity">The deleted entity.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected virtual async Task OnAfterDeleteAsync(T entity)
        {
            // Default implementation does nothing
            await Task.CompletedTask;
        }
    }
}