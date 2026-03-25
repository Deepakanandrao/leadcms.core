// <copyright file="TaskTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Configuration;
using LeadCMS.Data;
using LeadCMS.Elastic;
using LeadCMS.EmailSync.Tasks;
using LeadCMS.Helpers;
using LeadCMS.Infrastructure;
using LeadCMS.Interfaces;
using LeadCMS.Plugin.EmailSync.Data;
using LeadCMS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nest;

namespace LeadCMS.Tests;

public class TaskTests : BaseTestAutoLogin
{
    private const string TasksUrl = "/api/tasks";
    private const string EmailSyncTaskName = "EmailSyncTask";

    public TaskTests()
        : base()
    {
        TrackEntityType<DealPipeline>();
        TrackEntityType<TaskExecutionLog>();
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
    public async Task ExecuteStoppedEmailSyncTask_ShouldCreateManualLogWithResult()
    {
        using var scope = App.Services.CreateScope();
        var pgDbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();
        var httpContextHelper = scope.ServiceProvider.GetRequiredService<IHttpContextHelper>();
        var baseConfiguration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

        var configuration = new ConfigurationBuilder()
            .SetBasePath(repoRoot)
            .AddConfiguration(baseConfiguration)
            .AddJsonFile("plugins/LeadCMS.Plugin.EmailSync/pluginsettings.json", optional: false)
            .Build();

        var existingLogs = pgDbContext.TaskExecutionLogs!
            .Where(log => log.TaskName == EmailSyncTaskName)
            .ToList();

        if (existingLogs.Count > 0)
        {
            pgDbContext.TaskExecutionLogs!.RemoveRange(existingLogs);
            await pgDbContext.SaveChangesAsync();
        }

        var emailSyncDbOptions = new DbContextOptionsBuilder<PgDbContext>()
            .UseNpgsql(
                pgDbContext.Database.GetDbConnection().ConnectionString,
                options => options.MigrationsAssembly(typeof(EmailSyncDbContext).Assembly.FullName))
            .Options;

        await using var emailSyncDbContext = new EmailSyncDbContext(emailSyncDbOptions, configuration, httpContextHelper);
        await emailSyncDbContext.Database.MigrateAsync();

        var task = new EmailSyncTask(
            configuration,
            emailSyncDbContext,
            new TaskStatusService(),
            new StubDomainService(),
            new StubContactService());

        task.SetRunning(false);
        task.IsRunning.Should().BeFalse();

        var taskRunner = new TaskRunner(new[] { task }, pgDbContext);
        var completed = await taskRunner.ExecuteTask(task);
        completed.Should().BeTrue();

        var log = await pgDbContext.TaskExecutionLogs!
            .Where(entry => entry.TaskName == EmailSyncTaskName)
            .OrderByDescending(entry => entry.Id)
            .FirstOrDefaultAsync();

        log.Should().NotBeNull();
        log!.TaskName.Should().Be(EmailSyncTaskName);
        log.TriggeredBy.Should().Be(TaskExecutionTrigger.Manual);
        log.Status.Should().Be(TaskExecutionStatus.Completed);
        log.Result.Should().NotBeNullOrWhiteSpace();
        log.Result.Should().Contain("Processed 0 IMAP accounts");
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

    private sealed class StubDomainService : IDomainService
    {
        public string GetDomainNameByEmail(string email)
        {
            return email.Split('@').Last();
        }

        public Task Verify(Domain domain)
        {
            return Task.CompletedTask;
        }

        public Task SaveAsync(Domain item)
        {
            return Task.CompletedTask;
        }

        public Task SaveRangeAsync(List<Domain> items)
        {
            return Task.CompletedTask;
        }

        public void SetDBContext(PgDbContext pgDbContext)
        {
            _ = pgDbContext;
        }
    }

    private sealed class StubContactService : IContactService
    {
        public Task<Contact> FindOrCreate(string email, string? ipAddress = null, string? userAgent = null)
        {
            return Task.FromResult(new Contact { Email = email });
        }

        public Task<Contact> FindOrCreateByIdentifiers(string? email = null, string? phone = null, string? ipAddress = null, string? userAgent = null)
        {
            return Task.FromResult(new Contact { Email = email });
        }

        public Task<Contact> FindOrCreateByPhone(string phone, string? ipAddress = null, string? userAgent = null)
        {
            return Task.FromResult(new Contact());
        }

        public Task<Contact> FindOrCreatePotential(string ipAddress, string userAgent)
        {
            return Task.FromResult(new Contact());
        }

        public Task SaveAsync(Contact item)
        {
            return Task.CompletedTask;
        }

        public Task SaveRangeAsync(List<Contact> items)
        {
            return Task.CompletedTask;
        }

        public void SetDBContext(PgDbContext pgDbContext)
        {
            _ = pgDbContext;
        }

        public Task Subscribe(Contact contact, string groupName)
        {
            return Task.CompletedTask;
        }

        public Task Unsubscribe(string email, string reason, string source, DateTime? createdAt = null)
        {
            return Task.CompletedTask;
        }
    }
}