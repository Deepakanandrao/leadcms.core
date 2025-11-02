// <copyright file="TokenService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LeadCMS.Configuration;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace LeadCMS.Services;

/// <summary>
/// Service for managing JWT token operations and Microsoft token validation.
/// </summary>
public class TokenService : ITokenService
{
    private readonly IOptions<JwtConfig> jwtConfig;
    private readonly IOptions<AzureADConfig> azureAdConfig;
    private readonly UserManager<User> userManager;
    private readonly SignInManager<User> signInManager;
    private readonly IIdentityService identityService;
    private readonly IConfiguration configuration;
    private readonly ILogger<TokenService> logger;

    public TokenService(
        IOptions<JwtConfig> jwtConfig,
        IOptions<AzureADConfig> azureAdConfig,
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IIdentityService identityService,
        IConfiguration configuration,
        ILogger<TokenService> logger)
    {
        this.jwtConfig = jwtConfig;
        this.azureAdConfig = azureAdConfig;
        this.userManager = userManager;
        this.signInManager = signInManager;
        this.identityService = identityService;
        this.configuration = configuration;
        this.logger = logger;
    }

    public async Task<List<Claim>> ValidateMicrosoftTokenAsync(string microsoftToken)
    {
        var azureConfig = azureAdConfig.Value;
        if (!azureConfig.IsInitialized())
        {
            throw new IdentityException("Azure AD is not configured");
        }

        try
        {
            // Configure Microsoft token validation
            var instance = azureConfig.Instance.TrimEnd('/');
            var discoveryUrl = $"{instance}/{azureConfig.TenantId}/v2.0/.well-known/openid-configuration";

            logger.LogDebug("Attempting to retrieve Azure AD discovery document from: {DiscoveryUrl}", discoveryUrl);

            var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                discoveryUrl,
                new OpenIdConnectConfigurationRetriever());

            var discoveryDocument = await configurationManager.GetConfigurationAsync();

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = $"{instance}/{azureConfig.TenantId}/v2.0",
                ValidateAudience = true,
                ValidAudience = azureConfig.ClientId,
                ValidateLifetime = true,
                IssuerSigningKeys = discoveryDocument.SigningKeys,
                ClockSkew = TimeSpan.FromMinutes(5),
            };

            var tokenHandler = new JsonWebTokenHandler();
            var result = await tokenHandler.ValidateTokenAsync(microsoftToken, validationParameters);

            if (!result.IsValid)
            {
                logger.LogWarning("Microsoft token validation failed: {Exception}", result.Exception?.Message);
                throw new UnauthorizedException();
            }

            // Extract claims from the validated token
            var claimsIdentity = result.ClaimsIdentity;
            return claimsIdentity.Claims.ToList();
        }
        catch (Exception ex) when (!(ex is IdentityException || ex is UnauthorizedException))
        {
            var discoveryUrlForLogging = $"{azureConfig.Instance.TrimEnd('/')}/{azureConfig.TenantId}/v2.0/.well-known/openid-configuration";
            logger.LogError(ex, "Error validating Microsoft token. Discovery URL: {DiscoveryUrl}", discoveryUrlForLogging);

            // Check if it's a 404 error indicating the tenant or v2.0 endpoint doesn't exist
            if (ex.Message.Contains("IDX20807") || ex.Message.Contains("Unable to retrieve document"))
            {
                throw new IdentityException($"Azure AD discovery endpoint not found. Please verify the tenant ID '{azureConfig.TenantId}' is correct and the v2.0 endpoint is enabled for your tenant.");
            }

            throw new IdentityException("Failed to validate Microsoft token");
        }
    }

    public async Task<JWTokenDto> ExchangeTokenAsync(string microsoftToken)
    {
        // Validate the Microsoft token and extract claims
        var microsoftClaims = await ValidateMicrosoftTokenAsync(microsoftToken);

        // Extract email from Microsoft token
        var email = microsoftClaims
            .FirstOrDefault(c => c.Type.Contains("email") || c.Type == ClaimTypes.Email)?.Value;

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new IdentityException("Email not found in Microsoft token");
        }

        // Find or create user in our system
        var user = await identityService.FindOnRegister(email);

        if (await userManager.IsLockedOutAsync(user))
        {
            throw new IdentityException("Account locked out");
        }

        // Confirm email if not already confirmed (Microsoft has verified the email)
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
            logger.LogInformation("Email confirmed for user {Email} from Microsoft token validation", email);
        }

        // Check for Admin role in Microsoft token and update local user role if needed
        var administratorsGroupId = configuration.GetValue<string>("AzureAd:GroupsMapping:Administrators");
        if (!string.IsNullOrEmpty(administratorsGroupId))
        {
            // Azure AD tokens use "roles" claim type (not ClaimTypes.Role)
            var hasAdminClaim = microsoftClaims
                .Exists(c => c.Type == "roles" && c.Value == administratorsGroupId);

            logger.LogDebug(
                "Checking for admin role. Expected: {ExpectedRole}, Found roles: {ActualRoles}",
                administratorsGroupId,
                string.Join(", ", microsoftClaims.Where(c => c.Type == "roles").Select(c => c.Value)));

            if (hasAdminClaim)
            {
                // Check if user is already in Admin role
                var isInAdminRole = await userManager.IsInRoleAsync(user, "Admin");
                if (!isInAdminRole)
                {
                    var addRoleResult = await userManager.AddToRoleAsync(user, "Admin");
                    if (addRoleResult.Succeeded)
                    {
                        logger.LogInformation("Added Admin role to user {Email} from Microsoft token", email);
                    }
                    else
                    {
                        logger.LogWarning(
                            "Failed to add Admin role to user {Email}: {Errors}",
                            email,
                            string.Join(", ", addRoleResult.Errors.Select(e => e.Description)));
                    }
                }
            }
        }

        return await CompleteLoginAsync(user);
    }

    public async Task<JWTokenDto> LoginWithPasswordAsync(string email, string password)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user == null)
        {
            throw new UnauthorizedException();
        }

        if (!user.EmailConfirmed)
        {
            throw new IdentityException("Email is not confirmed");
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            throw new IdentityException("Account locked out");
        }

        var signResult = await signInManager.CheckPasswordSignInAsync(user, password, true);

        if (!signResult.Succeeded)
        {
            if (signResult.IsLockedOut)
            {
                throw new TooManyRequestsException();
            }
            else
            {
                throw new UnauthorizedException();
            }
        }

        return await CompleteLoginAsync(user);
    }

    public Task<JWTokenDto> GenerateTokenAsync(List<Claim> userClaims)
    {
        var config = jwtConfig.Value;
        if (!config.IsInitialized())
        {
            throw new IdentityException("JWT configuration is not initialized");
        }

        var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.Secret));

        var token = new JwtSecurityToken(
            issuer: config.Issuer,
            audience: config.Audience,
            expires: DateTime.UtcNow.AddYears(1),
            claims: userClaims,
            signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256));

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        logger.LogInformation(
            "Generated internal JWT token for user: {UserEmail}",
            userClaims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value);

        return Task.FromResult(new JWTokenDto
        {
            Token = tokenString,
            Expiration = token.ValidTo,
        });
    }

    private async Task<JWTokenDto> CompleteLoginAsync(User user)
    {
        // Update last login time
        user.LastTimeLoggedIn = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        // Create authentication claims
        var authClaims = new List<Claim>
        {
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, user.UserName!),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        };

        // Add all roles from the local user
        var roles = await userManager.GetRolesAsync(user);
        foreach (var role in roles)
        {
            authClaims.Add(new Claim(ClaimTypes.Role, role));
        }

        // Generate internal JWT token
        return await GenerateTokenAsync(authClaims);
    }
}