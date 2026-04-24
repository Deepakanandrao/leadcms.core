// <copyright file="IdentityTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Tests;

public class IdentityLoginTests : BaseTestAutoLogin
{
    [Theory]
    [InlineData("admin@admin.com", "")]
    [InlineData("UnexpectedUser@admin.com", "")]
    [InlineData("wrong address", "AnyPassword")]
    public async Task LoginBadParamsTest(string username, string password)
    {
        await TestBody(username, password, HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData("admin@admin.com", "WrongPassword")]
    [InlineData("UnexpectedUser@admin.com", "WrongPassword")]
    [InlineData("UnexpectedUser@admin.com", "adminPass!123")]
    public async Task LoginUnauthorizedTest(string username, string password)
    {
        await TestBody(username, password, HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("admin@admin.com", "WrongPassword")]
    [InlineData("admin@admin.com", "adminPass!123")]
    public async Task LoginNotConfirmedEmailTest(string username, string password)
    {
        using (var scope = App.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            Assert.NotNull(userManager);
            var user = await userManager.FindByEmailAsync(username);
            Assert.NotNull(user);

            // Store original value to restore later
            var originalEmailConfirmed = user.EmailConfirmed;

            user.EmailConfirmed = false;
            await userManager.UpdateAsync(user);

            try
            {
                await TestBody(username, password, HttpStatusCode.BadRequest);
            }
            finally
            {
                // Restore original state
                user.EmailConfirmed = originalEmailConfirmed;
                await userManager.UpdateAsync(user);
            }
        }
    }

    [Fact]
    public async Task LoginOkTest()
    {
        var token = await PostTest<JWTokenDto>(LoginApi, AdminLoginData, HttpStatusCode.OK);
        token.Should().NotBeNull();
        token!.Token.Should().NotBeEmpty();
    }

    [Fact]
    public async Task LoginLogoutTest()
    {
        string testApi = "/api/links";

        GetAuthenticationHeaderValue().Should().NotBeNull();
        await GetTest(testApi, HttpStatusCode.OK);

        Logout();
        GetAuthenticationHeaderValue().Should().BeNull();
        await GetTest(testApi, HttpStatusCode.Unauthorized);

        await LoginAsAdmin();
        GetAuthenticationHeaderValue().Should().NotBeNull();
        await GetTest(testApi, HttpStatusCode.OK);

        Logout();
        GetAuthenticationHeaderValue().Should().BeNull();
        await GetTest(testApi, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LockoutTest()
    {
        using var scope = App.Services.CreateScope();
        var lockoutConfig = scope.ServiceProvider.GetRequiredService<IConfiguration>()
            .GetSection("Identity").Get<IdentityConfig>();

        lockoutConfig.Should().NotBeNull();

        var testLoginDto = new LoginDto()
        { Email = AdminLoginData.Email, Password = "WrongPassword" };

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        try
        {
            // The first times login returns Unauthorized
            int count = lockoutConfig!.MaxFailedAccessAttempts - 1;
            for (int i = 0; i < count; i++)
            {
                await PostTest<JWTokenDto>(LoginApi, testLoginDto, HttpStatusCode.Unauthorized);
            }

            // When maximum number of failed attempts is achieved login returns TooManyRequests and blocks the user
            await PostTest<JWTokenDto>(LoginApi, testLoginDto, HttpStatusCode.TooManyRequests);
            // When the user is blocked login returns BadRequest
            await PostTest<JWTokenDto>(LoginApi, testLoginDto, HttpStatusCode.BadRequest);
            await PostTest<JWTokenDto>(LoginApi, testLoginDto, HttpStatusCode.BadRequest);

            // Re-fetch the user here so the entity has the current ConcurrencyStamp after all
            // the login attempts that updated AccessFailedCount/LockoutEnd via separate request scopes.
            // Using a stale entity would cause EF's optimistic concurrency check to silently fail,
            // leaving the lockout in place.
            var user = await userManager.FindByEmailAsync(AdminLoginData.Email);
            user.Should().NotBeNull();

            // Programmatically expire the lockout instead of waiting minutes.
            // ASP.NET Identity resets AccessFailedCount to 0 when the lockout is triggered,
            // so after expiry the next wrong-password attempt correctly returns Unauthorized.
            await userManager.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddSeconds(-1));

            await PostTest<JWTokenDto>(LoginApi, testLoginDto, HttpStatusCode.Unauthorized);
        }
        finally
        {
            // Always restore the admin user's lockout state so other tests are not affected,
            // regardless of whether this test passed or failed.
            var user = await userManager.FindByEmailAsync(AdminLoginData.Email);
            if (user != null)
            {
                await userManager.SetLockoutEndDateAsync(user, null);
                await userManager.ResetAccessFailedCountAsync(user);
            }
        }
    }

    private async Task TestBody(string username, string password, HttpStatusCode expectedCode)
    {
        var testLoginDto = new LoginDto()
        { Email = username, Password = password };
        Assert.NotEqual(testLoginDto, AdminLoginData);

        var token = await PostTest<JWTokenDto>(LoginApi, testLoginDto, expectedCode);
        token.Should().BeNull();
    }
}