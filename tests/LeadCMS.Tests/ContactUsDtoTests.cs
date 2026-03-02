// <copyright file="ContactUsDtoTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.Json;
using LeadCMS.Plugin.Site.DTOs;

namespace LeadCMS.Tests;

public class ContactUsDtoTests
{
    [Fact]
    public void Name_SingleWord_SetsFirstNameOnly()
    {
        var dto = new ContactUsDto { Name = "Alice", Message = "msg" };

        dto.FirstName.Should().Be("Alice");
        dto.MiddleName.Should().BeNull();
        dto.LastName.Should().BeNull();
    }

    [Fact]
    public void Name_TwoWords_SetsFirstAndLastName()
    {
        var dto = new ContactUsDto { Name = "Alice Smith", Message = "msg" };

        dto.FirstName.Should().Be("Alice");
        dto.MiddleName.Should().BeNull();
        dto.LastName.Should().Be("Smith");
    }

    [Fact]
    public void Name_ThreeWords_SetsFirstMiddleLastName()
    {
        var dto = new ContactUsDto { Name = "Alice B. Smith", Message = "msg" };

        dto.FirstName.Should().Be("Alice");
        dto.MiddleName.Should().Be("B.");
        dto.LastName.Should().Be("Smith");
    }

    [Fact]
    public void Name_FourWords_SetsLastNameAsRemainder()
    {
        var dto = new ContactUsDto { Name = "Alice B. C. Smith", Message = "msg" };

        dto.FirstName.Should().Be("Alice");
        dto.MiddleName.Should().Be("B.");
        dto.LastName.Should().Be("C. Smith");
    }

    [Fact]
    public void Name_DoesNotOverrideExplicitFirstName()
    {
        var dto = new ContactUsDto { FirstName = "Bob", Name = "Alice Smith", Message = "msg" };

        dto.FirstName.Should().Be("Bob");
        dto.LastName.Should().Be("Smith");
    }

    [Fact]
    public void Name_DoesNotOverrideExplicitLastName()
    {
        var dto = new ContactUsDto { LastName = "Jones", Name = "Alice Smith", Message = "msg" };

        dto.FirstName.Should().Be("Alice");
        dto.LastName.Should().Be("Jones");
    }

    [Fact]
    public void Name_DoesNotOverrideExplicitMiddleName()
    {
        var dto = new ContactUsDto { MiddleName = "Q.", Name = "Alice B. Smith", Message = "msg" };

        dto.FirstName.Should().Be("Alice");
        dto.MiddleName.Should().Be("Q.");
        dto.LastName.Should().Be("Smith");
    }

    [Fact]
    public void Name_NullOrWhitespace_DoesNothing()
    {
        var dto = new ContactUsDto { Name = null, Message = "msg" };
        dto.FirstName.Should().BeNull();

        dto.Name = "   ";
        dto.FirstName.Should().BeNull();

        dto.Name = string.Empty;
        dto.FirstName.Should().BeNull();
    }

    [Fact]
    public void Name_ExtraSpaces_AreTrimmedAndIgnored()
    {
        var dto = new ContactUsDto { Name = "  Alice   Smith  ", Message = "msg" };

        dto.FirstName.Should().Be("Alice");
        dto.LastName.Should().Be("Smith");
    }

    [Fact]
    public void FirstName_IsNullableAndNoLongerRequired()
    {
        var dto = new ContactUsDto { Message = "msg" };
        dto.FirstName.Should().BeNull();
    }

    [Fact]
    public void ExtraData_Deserialization_AcceptsMixedPrimitiveTypes()
    {
        var payload = "{\"Message\":\"msg\",\"ExtraData\":{\"pd_processing\":true,\"attempt\":3,\"ratio\":1.25,\"page\":\"tko-2\"}}";

        var dto = JsonSerializer.Deserialize<ContactUsDto>(payload);

        dto.Should().NotBeNull();
        dto!.ExtraData["pd_processing"].Should().Be("true");
        dto.ExtraData["attempt"].Should().Be("3");
        dto.ExtraData["ratio"].Should().Be("1.25");
        dto.ExtraData["page"].Should().Be("tko-2");
    }
}
