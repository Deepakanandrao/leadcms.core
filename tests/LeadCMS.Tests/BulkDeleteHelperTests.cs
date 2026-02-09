// <copyright file="BulkDeleteHelperTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Exceptions;
using LeadCMS.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace LeadCMS.Tests;

public class BulkDeleteHelperTests
{
    [Fact]
    public void ValidateIds_NullList_ReturnsUnprocessableEntity()
    {
        var result = BulkDeleteHelper.ValidateIds<int>(null);

        result.Should().NotBeNull();
        result.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public void ValidateIds_EmptyList_ReturnsUnprocessableEntity()
    {
        var result = BulkDeleteHelper.ValidateIds(Array.Empty<int>());

        result.Should().NotBeNull();
        result.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public void ValidateIds_NonEmptyList_ReturnsNull()
    {
        var result = BulkDeleteHelper.ValidateIds(new[] { 1, 2, 3 });

        result.Should().BeNull();
    }

    [Fact]
    public void ValidateIds_EmptyStringList_ReturnsUnprocessableEntity()
    {
        var result = BulkDeleteHelper.ValidateIds(Array.Empty<string>());

        result.Should().NotBeNull();
        result.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public void ValidateIds_NonEmptyStringList_ReturnsNull()
    {
        var result = BulkDeleteHelper.ValidateIds(new[] { "abc", "def" });

        result.Should().BeNull();
    }

    [Fact]
    public void ThrowIfMissingIds_AllFound_DoesNotThrow()
    {
        var action = () => BulkDeleteHelper.ThrowIfMissingIds("Test", new[] { 1, 2, 3 }, new[] { 1, 2, 3 });

        action.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfMissingIds_SomeMissing_ThrowsEntityNotFoundException()
    {
        var action = () => BulkDeleteHelper.ThrowIfMissingIds("Test", new[] { 1, 2, 3 }, new[] { 1 });

        action.Should().Throw<EntityNotFoundException>();
    }

    [Fact]
    public void ThrowIfMissingIds_AllMissing_ThrowsEntityNotFoundException()
    {
        var action = () => BulkDeleteHelper.ThrowIfMissingIds("Test", new[] { 1, 2 }, Array.Empty<int>());

        action.Should().Throw<EntityNotFoundException>();
    }

    [Fact]
    public void ThrowIfMissingIds_StringIds_SomeMissing_ThrowsEntityNotFoundException()
    {
        var action = () => BulkDeleteHelper.ThrowIfMissingIds("User", new[] { "a", "b", "c" }, new[] { "a" });

        action.Should().Throw<EntityNotFoundException>();
    }

    [Fact]
    public void ValidateIds_CustomDetail_IncludesDetailInResponse()
    {
        var result = BulkDeleteHelper.ValidateIds<int>(null, "Custom detail message");

        result.Should().NotBeNull();
        var objectResult = result as UnprocessableEntityObjectResult;
        objectResult.Should().NotBeNull();
        var problemDetails = objectResult!.Value as ProblemDetails;
        problemDetails.Should().NotBeNull();
        problemDetails!.Detail.Should().Be("Custom detail message");
    }
}
