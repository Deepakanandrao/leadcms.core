// <copyright file="IdentityController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LeadCMS.Configuration;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;
using LeadCMS.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LeadCMS.Controllers;

[AllowAnonymous]
[Route("api/[controller]")]
public class IdentityController : ControllerBase
{
    private readonly IOptions<AzureADConfig> azureAdConfig;
    private readonly UserManager<User> userManager;
    private readonly IEmailFromTemplateService emailFromTemplateService;
    private readonly ITokenService tokenService;
    private readonly IDeviceAuthService deviceAuthService;
    private readonly IIdentityService identityService;
    private readonly IConfiguration configuration;

    public IdentityController(
        IOptions<AzureADConfig> azureAdConfig,
        UserManager<User> userManager,
        IEmailService emailService,
        IEmailFromTemplateService emailFromTemplateService,
        ITokenService tokenService,
        IDeviceAuthService deviceAuthService,
        IIdentityService identityService,
        IConfiguration configuration)
    {
        this.azureAdConfig = azureAdConfig;
        this.userManager = userManager;
        this.emailFromTemplateService = emailFromTemplateService;
        this.tokenService = tokenService;
        this.deviceAuthService = deviceAuthService;
        this.identityService = identityService;
        this.configuration = configuration;
    }

    [HttpGet("azure-login")]
    public IActionResult AzureLogin(string returnUrl = "/")
    {
        // Check if Azure AD is properly configured
        if (string.IsNullOrEmpty(azureAdConfig.Value.TenantId) ||
            azureAdConfig.Value.TenantId == "$AZUREAD__TENANTID")
        {
            return BadRequest("Azure AD authentication is not configured.");
        }

        // Use the AzureAd OpenID Connect scheme for the challenge
        var redirectUri = Url.Action(nameof(AzureLoginCallback), new { returnUrl });
        var properties = new AuthenticationProperties { RedirectUri = redirectUri };

        // Challenge with Azure AD OpenID Connect scheme
        return Challenge(properties, "AzureAdOpenID");
    }

    [HttpGet("azure-login-callback")]
    public async Task<IActionResult> AzureLoginCallback(string returnUrl = "/")
    {
        // At this point, the user should be authenticated by Azure AD
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Unauthorized("Azure AD authentication failed.");
        }

        // Get the Azure AD access token if available
        var accessToken = await HttpContext.GetTokenAsync("AzureAdOpenID", "access_token");

        // Redirect back to the client application with the token
        // In a real implementation, you might need a more secure way to transmit the token
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect($"{returnUrl}?token={accessToken}");
        }

        // Token response for API clients
        return Ok(new { token = accessToken, tokenType = "Bearer" });
    }

    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> Login([FromBody] LoginDto input)
    {
        var token = await tokenService.LoginWithPasswordAsync(input.Email, input.Password);
        return Ok(token);
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user == null || !user.EmailConfirmed)
        {
            // Do not reveal user existence
            return Ok();
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);

        // Build admin URL (prefers AdminUrl config, then Origin header, then request)
        var adminBaseUrl = AppUrlHelper.GetAdminBaseUrl(configuration, Request);
        var resetUrl = $"{adminBaseUrl}/auth/reset-password?userId={user.Id}&token={Uri.EscapeDataString(token)}";

        var templateArgs = new Dictionary<string, object>
        {
            ["ResetUrl"] = resetUrl,
            ["UserName"] = user.UserName ?? string.Empty,
        };

        await emailFromTemplateService.SendAsync(
            "Password_Reset",
            dto.Language,
            new[] { user.Email! },
            templateArgs,
            null);

        return Ok();
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        var user = await userManager.FindByIdAsync(dto.UserId);
        if (user == null)
        {
            return BadRequest("Invalid user.");
        }

        var result = await userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);
        if (!result.Succeeded)
        {
            throw new IdentityException(result.Errors);
        }

        // Set email as confirmed since user successfully reset password using email token
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        return Ok();
    }

    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordDto value)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            throw new EntityNotFoundException(typeof(User).Name, User.Identity?.Name ?? string.Empty);
        }

        var passwordValid = await userManager.CheckPasswordAsync(user, value.CurrentPassword);
        if (!passwordValid)
        {
            throw new IdentityException("Current password is incorrect.");
        }

        var result = await userManager.ChangePasswordAsync(user, value.CurrentPassword, value.NewPassword);
        if (!result.Succeeded)
        {
            throw new IdentityException(result.Errors);
        }

        return Ok();
    }

    [HttpPost("exchange-token")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<JWTokenDto>> ExchangeToken([FromBody] TokenExchangeDto input)
    {
        try
        {
            var internalToken = await tokenService.ExchangeTokenAsync(input.MicrosoftToken);
            return Ok(internalToken);
        }
        catch (Exception ex) when (ex is IdentityException || ex is UnauthorizedException)
        {
            return Unauthorized(ex.Message);
        }
    }

    [HttpPost("device/initiate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DeviceAuthInitiateDto>> InitiateDeviceAuth()
    {
        var baseUrl = AppUrlHelper.GetAdminBaseUrl(configuration, Request);
        var deviceAuth = await deviceAuthService.InitiateDeviceAuthAsync(baseUrl);
        return Ok(deviceAuth);
    }

    [HttpPost("device/poll")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<JWTokenDto>> PollDeviceAuth([FromBody] DeviceAuthPollDto input)
    {
        var result = await deviceAuthService.PollDeviceAuthAsync(input.DeviceCode);

        return result.Status switch
        {
            DeviceAuthStatus.Completed when !string.IsNullOrEmpty(result.Token) => Ok(new JWTokenDto
            {
                Token = result.Token,
                Expiration = result.TokenExpiration ?? DateTime.UtcNow.AddYears(1),
            }),
            DeviceAuthStatus.Pending => Accepted(new { status = "authorization_pending", message = "User has not yet authorized the device" }),
            DeviceAuthStatus.Denied => BadRequest(new { error = "access_denied", error_description = result.ErrorDescription ?? "User denied the authorization request" }),
            DeviceAuthStatus.Expired => BadRequest(new { error = "expired_token", error_description = "Device code has expired" }),
            _ => BadRequest(new { error = "invalid_request", error_description = result.ErrorDescription ?? "Invalid device code or unexpected status" })
        };
    }

    [HttpPost("device/verify")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> VerifyDeviceAuth([FromBody] DeviceAuthVerificationDto input)
    {
        // Get current user claims
        var user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized("User not found");
        }

        var userClaims = await identityService.CreateUserClaims(user);

        // Verify the user code and associate with user
        var deviceCode = await deviceAuthService.VerifyUserCodeAsync(input.UserCode, userClaims);
        if (deviceCode == null)
        {
            return BadRequest(new { error = "invalid_user_code", error_description = "Invalid or expired user code" });
        }

        // Generate token for the device auth
        var tokenResult = await tokenService.GenerateTokenAsync(userClaims);

        // Complete the device auth with the token
        var completed = await deviceAuthService.CompleteDeviceAuthAsync(deviceCode, tokenResult.Token, tokenResult.Expiration);
        if (!completed)
        {
            return BadRequest(new { error = "completion_failed", error_description = "Failed to complete device authorization" });
        }

        return Ok(new { message = "Device authorization completed successfully. You can now close this window." });
    }

    [HttpPost("device/deny")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> DenyDeviceAuth([FromBody] DeviceAuthVerificationDto input)
    {
        var denied = await deviceAuthService.DenyDeviceAuthAsync(input.UserCode, "User denied authorization");
        if (!denied)
        {
            return BadRequest(new { error = "invalid_user_code", error_description = "Invalid or expired user code" });
        }

        return Ok(new { message = "Device authorization denied successfully." });
    }
}