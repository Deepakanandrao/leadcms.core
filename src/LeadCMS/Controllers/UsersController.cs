// <copyright file="UsersController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Configuration;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Infrastructure;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LeadCMS.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    protected readonly PgDbContext dbContext;
    protected readonly IMapper mapper;
    private readonly UserManager<User> userManager;
    private readonly IEmailFromTemplateService emailFromTemplateService;
    private readonly IdentityConfig identityConfig;
    private readonly UserQueryProvider userQueryProvider;

    public UsersController(
        PgDbContext dbContext,
        IMapper mapper,
        UserManager<User> userManager,
        IEmailFromTemplateService emailFromTemplateService,
        IOptions<IdentityConfig> identityOptions,
        UserQueryProvider userQueryProvider)
    {
        this.dbContext = dbContext;
        this.mapper = mapper;
        this.userManager = userManager;
        this.emailFromTemplateService = emailFromTemplateService;
        identityConfig = identityOptions.Value;
        this.userQueryProvider = userQueryProvider;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserDetailsDto[]>> GetAll([FromQuery] string? query)
    {
        var result = await userQueryProvider.GetResult(userManager.Users);
        var resultsToClient = mapper.Map<UserDetailsDto[]>(result.Records).ToArray();
        Response.Headers.Append(ResponseHeaderNames.TotalCount, result.TotalCount.ToString());
        Response.Headers.Append(ResponseHeaderNames.AccessControlExposeHeader, ResponseHeaderNames.TotalCount);
        return Ok(resultsToClient);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserDetailsDto>> GetSpecific(string id)
    {
        var existingEntity = await userManager.FindByIdAsync(id);
        if (existingEntity == null)
        {
            throw new EntityNotFoundException(typeof(User).Name, id);
        }

        return Ok(mapper.Map<UserDetailsDto>(existingEntity));
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public virtual async Task<ActionResult<UserDetailsDto>> Post([FromBody] UserCreateDto userDto)
    {
        var newUser = mapper.Map<User>(userDto);

        // Automatically set email as verified for admin-created users
        newUser.EmailConfirmed = true;

        // Set CreatedAt to current UTC time to prevent DateTime.MinValue issue
        newUser.CreatedAt = DateTime.UtcNow;

        string password = userDto.Password ?? string.Empty;
        if (userDto.GeneratePassword || string.IsNullOrEmpty(password))
        {
            password = PasswordHelper.GenerateStrongPassword(identityConfig);
        }

        var result = await userManager.CreateAsync(newUser, password);
        if (!result.Succeeded)
        {
            throw new IdentityException(result.Errors);
        }

        // Assign Admin role to admin-created users
        var roleResult = await userManager.AddToRoleAsync(newUser, "Admin");
        if (!roleResult.Succeeded)
        {
            throw new IdentityException(roleResult.Errors);
        }

        if (userDto.SendPasswordEmail)
        {
            var args = new Dictionary<string, object>
            {
                ["UserName"] = newUser.UserName ?? newUser.Email ?? string.Empty,
                ["Password"] = password,
            };
            await emailFromTemplateService.SendAsync("Account_Created", userDto.Language, new[] { newUser.Email! }, args, null);
        }

        var createdUser = await userManager.FindByNameAsync(userDto.UserName);
        var createdUserDto = mapper.Map<UserDetailsDto>(createdUser);

        return CreatedAtAction(nameof(GetSpecific), new { id = createdUser!.Id }, createdUserDto);
    }

    [HttpPatch("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public virtual async Task<ActionResult<UserDetailsDto>> Patch(string id, [FromBody] UserUpdateDto userDto)
    {
        var existingEntity = await userManager.FindByIdAsync(id);
        if (existingEntity == null)
        {
            throw new EntityNotFoundException(typeof(User).Name, id);
        }

        mapper.Map(userDto, existingEntity);

        string? password = userDto.Password;
        if (userDto.GeneratePassword)
        {
            password = PasswordHelper.GenerateStrongPassword(identityConfig);
        }

        if (!string.IsNullOrEmpty(password))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(existingEntity);
            var result = await userManager.ResetPasswordAsync(existingEntity, token, password);

            if (!result.Succeeded)
            {
                throw new IdentityException(result.Errors);
            }

            if (userDto.SendPasswordEmail)
            {
                var args = new Dictionary<string, object>
                {
                    ["UserName"] = existingEntity.UserName ?? existingEntity.Email ?? string.Empty,
                    ["Password"] = password,
                };
                await emailFromTemplateService.SendAsync("Password_Updated", userDto.Language, new[] { existingEntity.Email! }, args, null);
            }
        }

        await dbContext.SaveChangesAsync();

        var resultsToClient = mapper.Map<UserDetailsDto>(existingEntity);

        return Ok(resultsToClient);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public virtual async Task<ActionResult> Delete(string id)
    {
        var existingEntity = await userManager.FindByIdAsync(id);
        if (existingEntity == null)
        {
            throw new EntityNotFoundException(typeof(User).Name, id);
        }

        var result = await userManager.DeleteAsync(existingEntity);
        if (result.Errors.Any())
        {
            throw new IdentityException(result.Errors);
        }

        return NoContent();
    }

    [HttpDelete("bulk")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public virtual async Task<ActionResult> DeleteMany([FromBody] List<string> ids)
    {
        var invalidResult = BulkDeleteHelper.ValidateIds(ids);
        if (invalidResult != null)
        {
            return invalidResult;
        }

        var distinctIds = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct()
            .ToList();

        var usersToDelete = await userManager.Users
            .Where(user => distinctIds.Contains(user.Id))
            .ToListAsync();

        BulkDeleteHelper.ThrowIfMissingIds(typeof(User).Name, distinctIds, usersToDelete.Select(user => user.Id));

        foreach (var user in usersToDelete)
        {
            var result = await userManager.DeleteAsync(user);
            if (result.Errors.Any())
            {
                throw new IdentityException(result.Errors);
            }
        }

        return NoContent();
    }
}