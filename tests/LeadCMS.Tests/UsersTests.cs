// <copyright file="UsersTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Helpers;
using LeadCMS.Tests.TestEntities;

namespace LeadCMS.Tests;

public class UsersTests : BaseTestAutoLogin
{
    private readonly string usersUrl = "/api/users";

    [Fact]
    public async Task CreateUser_ShouldSetCreatedAtCorrectly()
    {
        // Arrange
        var userCreateDto = new TestUser("1")
        {
            Password = "TestPassword123!", // Override generated password for this specific test
            GeneratePassword = false,
        };

        // Act
        var response = await Request(HttpMethod.Post, usersUrl, userCreateDto);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var content = await response.Content.ReadAsStringAsync();
        var createdUser = JsonHelper.Deserialize<UserDetailsDto>(content);

        // Assert
        createdUser.Should().NotBeNull();
        createdUser!.Email.Should().Be(userCreateDto.Email);
        createdUser.UserName.Should().Be(userCreateDto.UserName);
        createdUser.DisplayName.Should().Be(userCreateDto.DisplayName);

        // Validate that CreatedAt is set to a proper date (not DateTime.MinValue which shows as "01.01.1")
        createdUser.CreatedAt.Should().NotBe(DateTime.MinValue);
        createdUser.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        // LastTimeLoggedIn should be null for newly created users who haven't logged in yet
        createdUser.LastTimeLoggedIn.Should().BeNull();
    }

    [Fact]
    public async Task CreateUser_WithGeneratedPassword_ShouldSetCreatedAtCorrectly()
    {
        // Arrange
        var userCreateDto = new TestUser("2"); // Uses GeneratePassword = true by default

        // Act
        var response = await Request(HttpMethod.Post, usersUrl, userCreateDto);

        // If it failed, log the error for debugging
        if (response.StatusCode != HttpStatusCode.Created)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"GeneratePassword Test Error Response: {errorContent}");
        }

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var content = await response.Content.ReadAsStringAsync();
        var createdUser = JsonHelper.Deserialize<UserDetailsDto>(content);

        // Assert
        createdUser.Should().NotBeNull();
        createdUser!.Email.Should().Be(userCreateDto.Email);

        // Validate that CreatedAt is set to a proper date (not DateTime.MinValue)
        createdUser.CreatedAt.Should().NotBe(DateTime.MinValue);
        createdUser.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetAllUsers_ShouldReturnUsersWithProperCreatedAt()
    {
        // Arrange - Create a test user first
        var userCreateDto = new TestUser("3");

        var createResponse = await Request(HttpMethod.Post, usersUrl, userCreateDto);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act
        var users = await GetTest<UserDetailsDto[]>(usersUrl);

        // Assert
        users.Should().NotBeNull();
        users!.Length.Should().BeGreaterThan(0);

        // Find our test user
        var testUser = users.FirstOrDefault(u => u.Email == userCreateDto.Email);
        testUser.Should().NotBeNull();

        // Validate that CreatedAt is not DateTime.MinValue for our test user
        testUser!.CreatedAt.Should().NotBe(DateTime.MinValue);
        testUser.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetSpecificUser_ShouldReturnUserWithProperCreatedAt()
    {
        // Arrange - Create a test user first
        var userCreateDto = new TestUser("4");

        var createResponse = await Request(HttpMethod.Post, usersUrl, userCreateDto);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createdUser = JsonHelper.Deserialize<UserDetailsDto>(createContent);

        // Act
        var retrievedUser = await GetTest<UserDetailsDto>($"{usersUrl}/{createdUser!.Id}");

        // Assert
        retrievedUser.Should().NotBeNull();
        retrievedUser!.Email.Should().Be(userCreateDto.Email);

        // Validate that CreatedAt is not DateTime.MinValue
        retrievedUser.CreatedAt.Should().NotBe(DateTime.MinValue);
        retrievedUser.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        // CreatedAt should match what was returned during creation (PostgreSQL has microsecond precision)
        retrievedUser.CreatedAt.Should().BeCloseTo(createdUser.CreatedAt, TimeSpan.FromMilliseconds(1));
    }
}