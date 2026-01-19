// <copyright file="TestContact.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Tests.TestEntities;

public class TestContact : ContactCreateDto
{
    public TestContact()
        : this(string.Empty)
    {
    }

    public TestContact(string uid)
    {
        Email = $"contact{uid}@test{uid}.net";
        FirstName = $"FirstName_{uid}";
        LastName = $"LastName_{uid}";
        Language = "en";
    }
}