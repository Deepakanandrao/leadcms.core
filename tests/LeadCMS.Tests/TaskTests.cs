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
using LeadCMS.Plugin.EmailSync.Configuration;
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
        var domainService = scope.ServiceProvider.GetRequiredService<IDomainService>();
        var contactService = scope.ServiceProvider.GetRequiredService<IContactService>();

        var configuration = new ConfigurationBuilder()
            .AddConfiguration(baseConfiguration)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EmailSync:EncryptionKey"] = "test-key-123456!",
                ["EmailSync:CreateContactsForUnknownEmails"] = bool.TrueString,
                ["Tasks:EmailSyncTask:Enable"] = bool.FalseString,
                ["Tasks:EmailSyncTask:CronSchedule"] = "0/30 * * * * ?",
                ["Tasks:EmailSyncTask:RetryCount"] = "2",
                ["Tasks:EmailSyncTask:RetryInterval"] = "1",
                ["Tasks:EmailSyncTask:BatchSize"] = "20",
            })
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
            domainService,
            contactService);

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
    public async Task ExecuteEmailSyncTask_WithProductionPlaceholderKey_ShouldFailAndStoreResult()
    {
        using var scope = App.Services.CreateScope();
        var pgDbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();
        var httpContextHelper = scope.ServiceProvider.GetRequiredService<IHttpContextHelper>();
        var baseConfiguration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var domainService = scope.ServiceProvider.GetRequiredService<IDomainService>();
        var contactService = scope.ServiceProvider.GetRequiredService<IContactService>();

        var configuration = new ConfigurationBuilder()
            .AddConfiguration(baseConfiguration)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["EmailSync:EncryptionKey"] = EmailSyncConfigurationValidator.EncryptionKeyPlaceholder,
                ["EmailSync:CreateContactsForUnknownEmails"] = bool.TrueString,
                ["Tasks:EmailSyncTask:Enable"] = bool.FalseString,
                ["Tasks:EmailSyncTask:CronSchedule"] = "0/30 * * * * ?",
                ["Tasks:EmailSyncTask:RetryCount"] = "2",
                ["Tasks:EmailSyncTask:RetryInterval"] = "1",
                ["Tasks:EmailSyncTask:BatchSize"] = "20",
            })
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

        var task = new EmailSyncTask(
            configuration,
            emailSyncDbContext,
            new TaskStatusService(),
            domainService,
            contactService);

        var taskRunner = new TaskRunner(new[] { task }, pgDbContext);
        var completed = await taskRunner.ExecuteTask(task);
        completed.Should().BeFalse();

        var log = await pgDbContext.TaskExecutionLogs!
            .Where(entry => entry.TaskName == EmailSyncTaskName)
            .OrderByDescending(entry => entry.Id)
            .FirstOrDefaultAsync();

        log.Should().NotBeNull();
        log!.Status.Should().Be(TaskExecutionStatus.Pending);
        log.Result.Should().Contain("placeholder value");
        log.Result.Should().Contain(EmailSyncConfigurationValidator.EncryptionKeyPlaceholder);
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