// <copyright file="TaskTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Configuration;
using LeadCMS.Elastic;
using LeadCMS.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nest;

namespace LeadCMS.Tests;

public class TaskTests : BaseTestAutoLogin
{
    private const string TasksUrl = "/api/tasks";

    public TaskTests()
        : base()
    {
        TrackEntityType<DealPipeline>();
    }

    [Fact]
    public async Task GetAllTasksTest()
    {
        var responce = await GetRequest(TasksUrl);

        var content = await responce.Content.ReadAsStringAsync();

        var tasks = JsonHelper.Deserialize<IList<TaskDetailsDto>>(content);

        tasks.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetByNameFailureTest()
    {
        await GetTest(TasksUrl + "/SomeUnexistedTask", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByNameSuccesTest()
    {
        var name = "SyncEsTask";

        var responce = await GetTest<TaskDetailsDto>(TasksUrl + "/" + name);

        responce.Should().NotBeNull();
        responce!.Name.Should().Contain("SyncEsTask");
    }

    [Fact]
    public async Task StartAndStopTaskTest()
    {
        var name = "SyncEsTask";

        var responce = await GetTest<TaskDetailsDto>(TasksUrl + "/" + name);
        responce.Should().NotBeNull();
        responce!.IsRunning.Should().BeFalse();

        responce = await GetTest<TaskDetailsDto>(TasksUrl + "/start/" + name);
        responce.Should().NotBeNull();
        responce!.IsRunning.Should().BeTrue();

        responce = await GetTest<TaskDetailsDto>(TasksUrl + "/stop/" + name);
        responce.Should().NotBeNull();
        responce!.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAllChangeLogRecordsTest()
    {
        await CheckIfTaskNotRunning("SyncEsTask");

        var config = App.Services.GetRequiredService<IConfiguration>();
        config.Should().NotBeNull();
        var esSyncBatchSize = config.GetSection("Tasks:SyncEsTask")!.Get<TaskWithBatchConfig>()!.BatchSize;

        PopulateBulkData<DealPipeline, IEntityService<DealPipeline>>(mapper.Map<List<DealPipeline>>(TestData.GenerateAndPopulateAttributes<TestDealPipeline>(esSyncBatchSize * 2, null)));

        await SyncElasticSearch();

        CountDocumentsInIndex(GetIndexName<DealPipeline>()).Should().Be(esSyncBatchSize * 2);
    }

    [Fact]
    public async Task ReindexElasticAfterDeletingIndex()
    {
        int dataSize = 10;

        await CheckIfTaskNotRunning("SyncEsTask");

        PopulateBulkData<DealPipeline, IEntityService<DealPipeline>>(mapper.Map<List<DealPipeline>>(TestData.GenerateAndPopulateAttributes<TestDealPipeline>(dataSize, null)));

        await SyncElasticSearch();

        var indexName = GetIndexName<DealPipeline>();
        CountDocumentsInIndex(indexName).Should().Be(dataSize);

        App.GetElasticClient().Indices.Delete(indexName);

        await SyncElasticSearch();

        CountDocumentsInIndex(indexName).Should().Be(dataSize);
    }

    private async Task CheckIfTaskNotRunning(string taskName)
    {
        var responce = await GetTest<TaskDetailsDto>(TasksUrl + "/" + taskName);
        responce.Should().NotBeNull();
        responce!.IsRunning.Should().BeFalse();
    }

    private long CountDocumentsInIndex(string indexName)
    {
        var elasticClient = App.GetElasticClient();
        var countResponse = elasticClient.Count(new CountRequest(Indices.Index(indexName)));
        return countResponse.Count;
    }

    private string GetIndexName<T>()
        where T : class
    {
        var config = App.Services.GetRequiredService<IConfiguration>();
        var indexPrefix = config.GetSection("Elastic:IndexPrefix").Get<string>() ?? string.Empty;
        return ElasticHelper.GetIndexName(indexPrefix, typeof(T));
    }
}