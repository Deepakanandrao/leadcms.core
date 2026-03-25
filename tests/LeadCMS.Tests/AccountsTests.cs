// <copyright file="AccountsTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Tests;

public class AccountsTests : SimpleTableTests<Account, TestAccount, AccountUpdateDto, IEntityService<Account>>
{
    public AccountsTests()
        : base("/api/accounts")
    {
    }

    [Fact]
    public async Task CheckTags()
    {
        var tag = $"account-tag-{Guid.NewGuid():N}";
        var account = new TestAccount(Guid.NewGuid().ToString("N"))
        {
            Tags = new[] { tag },
        };

        await PostTest(itemsUrl, account, HttpStatusCode.Created);

        var tags = await GetTest<string[]>($"{itemsUrl}/tags", HttpStatusCode.OK);
        tags.Should().NotBeNull();
        tags.Should().Contain(tag);
    }

    protected override AccountUpdateDto UpdateItem(TestAccount to)
    {
        var from = new AccountUpdateDto();
        to.Name = from.Name = to.Name + "Updated";
        return from;
    }
}