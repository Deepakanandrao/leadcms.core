// <copyright file="SimpleTableTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Web;
using LeadCMS.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Tests;

public abstract class SimpleTableTests<T, TC, TU, TS> : BaseTestAutoLogin
    where T : BaseEntityWithId
    where TC : class
    where TU : new()
    where TS : IEntityService<T>
{
    protected readonly string itemsUrl;
    protected readonly string itemsUrlNotFound;

    protected SimpleTableTests(string url)
    {
        itemsUrl = url;
        itemsUrlNotFound = url + "/404";
        TrackEntityType<T>();
    }

    [Fact]
    public async Task GetAllTest()
    {
        await GetAllRecords(false);
    }

    [Fact]
    public async Task GetItemNotFoundTest()
    {
        await GetTest(itemsUrlNotFound, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateAndGetItemTest()
    {
        await CreateAndGetItem(false);
    }

    [Fact]
    public virtual async Task UpdateItemNotFoundTest()
    {
        var testCreateItem = await CreateItem();

        var testUpdateItem = UpdateItem(testCreateItem.Item1);

        await PatchTest(itemsUrlNotFound, testUpdateItem!, HttpStatusCode.NotFound);
    }

    [Fact]
    public virtual async Task CreateAndCheckEntityState_ChangeLog()
    {
        var testCreateItem = await CreateItem();

        var item = await GetTest<T>(testCreateItem.Item2);

        var result = App.GetDbContext()!.ChangeLogs!.FirstOrDefault(c => c.ObjectId == item!.Id && c.ObjectType == typeof(T).Name && c.EntityState == EntityState.Added)!;

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAndUpdateItemTest()
    {
        var createAndUpdateItems = await CreateAndUpdateItem();

        var item = await GetTest<T>(createAndUpdateItems.testCreateItem.Item2);

        MustBeEquivalent(createAndUpdateItems.testCreateItem.Item1, item);
    }

    [Fact]
    public virtual async Task CreateAndUpdateCheckEntityState_ChangeLog()
    {
        var createAndUpdateItems = await CreateAndUpdateItem();

        var item = await GetTest<T>(createAndUpdateItems.testCreateItem.Item2);

        var result = App.GetDbContext()!.ChangeLogs!.FirstOrDefault(c => c.ObjectId == item!.Id && c.ObjectType == typeof(T).Name && c.EntityState == EntityState.Modified)!;

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteItemNotFoundTest()
    {
        await DeleteTest(itemsUrlNotFound, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateAndDeleteItemTest()
    {
        var testCreateItem = await CreateItem();

        await DeleteTest(testCreateItem.Item2);

        await GetTest(testCreateItem.Item2, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BulkDeleteItemsTest()
    {
        var item1 = await CreateItem();
        var item2 = await CreateItem();

        var id1 = int.Parse(item1.Item2.Split('/').Last());
        var id2 = int.Parse(item2.Item2.Split('/').Last());

        await DeleteTest($"{itemsUrl}/bulk", new[] { id1, id2 });

        await GetTest(item1.Item2, HttpStatusCode.NotFound);
        await GetTest(item2.Item2, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BulkDeleteItems_EmptyBody_ShouldReturnUnprocessableEntity()
    {
        await DeleteTest($"{itemsUrl}/bulk", Array.Empty<int>(), HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task BulkDeleteItems_NonExistentIds_ShouldReturnNotFound()
    {
        await DeleteTest($"{itemsUrl}/bulk", new[] { 999998, 999999 }, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateAndDeleteCheckEntityState_ChangeLog()
    {
        var testCreateItem = await CreateItem();

        var item = await GetTest<T>(testCreateItem.Item2);

        await DeleteTest(testCreateItem.Item2);

        var result = App.GetDbContext()!.ChangeLogs!.FirstOrDefault(c => c.ObjectId == item!.Id && c.ObjectType == typeof(T).Name && c.EntityState == EntityState.Deleted)!;

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidWherePropertyType()
    {
        var query = string.Empty;
        var typeProperties = typeof(T).GetProperties();
        foreach (var property in typeProperties)
        {
            if (!property.PropertyType.IsValueType || (Nullable.GetUnderlyingType(property.PropertyType) != null))
            {
                continue;
            }

            object? defValue;
            if (property.PropertyType == typeof(string))
            {
                defValue = "abc";
            }
            else if (property.PropertyType == typeof(DateTime))
            {
                defValue = DateTime.MinValue.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK");
            }
            else
            {
                defValue = Activator.CreateInstance(property.PropertyType);
            }

            query += HttpUtility.UrlEncode($"filter[where][{property.Name}][eq]={defValue}") + "&";
        }

        query = query.Substring(0, query.Length - 1); // Remove latest '&'
        await GetTest($"{itemsUrl}?{query}", HttpStatusCode.OK);
    }

    [Fact]
    public async Task InvalidWherePropertyType()
    {
        var baseTypesList = new Type[]
        {
            typeof(string),
            typeof(DateTime),
            typeof(int),
        };
        var query = string.Empty;
        var typeProperties = typeof(T).GetProperties();
        foreach (var property in typeProperties)
        {
            if (!property.PropertyType.IsValueType
                || (Nullable.GetUnderlyingType(property.PropertyType) != null)
                || property.PropertyType == typeof(decimal) // Default value for decimal, double, float, long serializes as 0 so skip them
                || property.PropertyType == typeof(double)
                || property.PropertyType == typeof(float)
                || property.PropertyType == typeof(long))
            {
                continue;
            }

            query += baseTypesList.Where(t => t != property.PropertyType).Select(type =>
            {
                object? defValue;
                if (type == typeof(string))
                {
                    defValue = "abc";
                }
                else if (type == typeof(DateTime))
                {
                    defValue = DateTime.MinValue.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK");
                }
                else
                {
                    defValue = Activator.CreateInstance(type);
                }

                return HttpUtility.UrlEncode($"filter[where][{property.Name}][eq]={defValue}") + "&";
            }).Aggregate(string.Empty, (acc, value) => acc + value);
        }

        query = query.Substring(0, query.Length - 1); // Remove latest '&'
        var queryCmds = query.Split('&').Select(s => HttpUtility.UrlDecode(s)).ToList();
        var queryCmdsCount = queryCmds.Count;

        var result = await GetTestRawContentSerialize<ProblemDetails>($"{itemsUrl}?{query}", HttpStatusCode.BadRequest);
        result.Should().NotBeNull();
        var resultDiff = queryCmds.Except(result!.Extensions.Keys).Aggregate(string.Empty, (acc, value) => $"{acc} \n {value}");
        result!.Extensions.Count(pair => pair.Key.ToLowerInvariant() != "traceid").Should().Be(queryCmdsCount, resultDiff);
    }

    protected virtual void MustBeEquivalent(object? expected, object? result)
    {
        result.Should().BeEquivalentTo(expected);
    }

    protected virtual async Task<(TC, string)> CreateItem()
    {
        var testCreateItem = TestData.Generate<TC>(Guid.NewGuid().ToString("N")[..8]);

        // Track the entity type being created
        TrackEntityType<T>();

        var newItemUrl = await PostTest(itemsUrl, testCreateItem, HttpStatusCode.Created);

        return (testCreateItem, newItemUrl);
    }

    protected virtual void GenerateBulkRecords(int dataCount, Action<TC>? populateAttributes = null)
    {
        var bulkList = TestData.GenerateAndPopulateAttributes<TC>(dataCount, populateAttributes);
        var bulkEntitiesList = mapper.Map<List<T>>(bulkList);

        PopulateBulkData<T, TS>(bulkEntitiesList);
    }

    protected async Task GetAllRecords(bool asAnonimus)
    {
        const int numberOfItems = 10;

        GenerateBulkRecords(numberOfItems);

        if (asAnonimus)
        {
            Logout();
        }

        var items = await GetTest<List<T>>(itemsUrl, HttpStatusCode.OK);

        items.Should().NotBeNull();
        items!.Count.Should().Be(numberOfItems);
    }

    protected async Task CreateAndGetItem(bool asAnonimus)
    {
        var testCreateItem = await CreateItem();

        if (asAnonimus)
        {
            Logout();
        }

        var item = await GetTest<T>(testCreateItem.Item2, HttpStatusCode.OK);

        MustBeEquivalent(testCreateItem.Item1, item);
    }

    protected abstract TU UpdateItem(TC createdItem);

    private async Task<((TC, string) testCreateItem, TU? testUpdateItem)> CreateAndUpdateItem()
    {
        var testCreateItem = await CreateItem();

        var testUpdateItem = UpdateItem(testCreateItem.Item1);

        await PatchTest(testCreateItem.Item2, testUpdateItem!);

        return (testCreateItem, testUpdateItem);
    }

    private async Task<TRet?> GetTestRawContentSerialize<TRet>(string url, HttpStatusCode expectedCode = HttpStatusCode.OK)
    where TRet : class
    {
        var response = await GetTest(url, expectedCode);

        var content = await response.Content.ReadAsStringAsync();

        return JsonHelper.Deserialize<TRet>(content);
    }
}