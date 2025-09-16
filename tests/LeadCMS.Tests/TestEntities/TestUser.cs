// <copyright file="TestUser.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.DTOs;

namespace LeadCMS.Tests.TestEntities;

public class TestUser : UserCreateDto
{
    public TestUser(string uid = "")
    {
        Email = $"testuser{uid}@example.com";
        UserName = $"testuser{uid}";
        DisplayName = $"Test User {uid}";
        GeneratePassword = true;
        SendPasswordEmail = false;
        Language = "en";
        Data = new Dictionary<string, object>
        {
            { "testData", $"test-value-{uid}" },
            { "createdBy", "TestUser" },
        };
    }
}