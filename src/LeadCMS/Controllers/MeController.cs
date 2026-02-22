// <copyright file="MeController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Configuration;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;

namespace LeadCMS.Controllers;

[Authorize]
[Route("api/users")]
public class MeController : ControllerBase
{
    private readonly IMapper mapper;
    private readonly UserManager<User> userManager;
    private readonly IEmailFromTemplateService emailFromTemplateService;
    private readonly IdentityConfig identityConfig;

    public MeController(
        IMapper mapper,
        UserManager<User> userManager,
        IEmailFromTemplateService emailFromTemplateService,
        IOptions<IdentityConfig> identityOptions)
    {
        this.mapper = mapper;
        this.userManager = userManager;
        this.emailFromTemplateService = emailFromTemplateService;
        identityConfig = identityOptions.Value;
    }

    [HttpGet("me")]
    [SwaggerOperation(Tags = new[] { "Users" })]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserDetailsDto>> GetSelf()
    {
        var user = await UserHelper.GetCurrentUserOrThrowAsync(userManager, User);

        return Ok(mapper.Map<UserDetailsDto>(user));
    }

    [HttpPatch("me")]
    [SwaggerOperation(Tags = new[] { "Users" })]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public virtual async Task<ActionResult<UserDetailsDto>> Patch([FromBody] UserUpdateDto value)
    {
        var user = await UserHelper.GetCurrentUserOrThrowAsync(userManager, User);

        mapper.Map(value, user);

        string? password = value.Password;
        if (value.GeneratePassword)
        {
            password = PasswordHelper.GenerateStrongPassword(identityConfig);
        }

        if (!string.IsNullOrEmpty(password))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var result = await userManager.ResetPasswordAsync(user, token, password);

            if (!result.Succeeded)
            {
                throw new IdentityException(result.Errors);
            }

            if (value.SendPasswordEmail)
            {
                var args = new Dictionary<string, object>
                {
                    ["UserName"] = user.UserName ?? user.Email ?? string.Empty,
                    ["Password"] = password,
                };
                await emailFromTemplateService.SendAsync("Password_Updated", value.Language, new[] { user.Email! }, args, null);
            }
        }

        await userManager.UpdateAsync(user);

        var userDetails = mapper.Map<UserDetailsDto>(user);

        return Ok(userDetails);
    }
}