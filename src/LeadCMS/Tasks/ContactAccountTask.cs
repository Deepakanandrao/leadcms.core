// <copyright file="ContactAccountTask.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Configuration;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Interfaces;
using LeadCMS.Services;

namespace LeadCMS.Tasks;

public class ContactAccountTask : BaseTask
{
    private const string ConfigKey = "Tasks:ContactAccountTask";

    protected readonly PgDbContext dbContext;

    private readonly IAccountExternalService accountExternalService;

    private readonly IMapper mapper;

    private readonly int batchSize;

    public ContactAccountTask(IConfiguration configuration, TaskStatusService taskStatusService, PgDbContext dbContext, IAccountExternalService accountExternalService, IMapper mapper)
        : base(ConfigKey, configuration, taskStatusService)
    {
        this.dbContext = dbContext;
        this.accountExternalService = accountExternalService;
        this.mapper = mapper;

        var config = configuration.GetSection(ConfigKey)!.Get<TaskWithBatchConfig>();

        if (config is not null)
        {
            batchSize = config.BatchSize;
        }
        else
        {
            throw new MissingConfigurationException($"The specified configuration section for the provided ConfigKey {ConfigKey} could not be found in the settings file.");
        }
    }

    public override async Task<bool> Execute(TaskExecutionLog currentJob)
    {
        try
        {
            int totalDomains = 0;
            int totalContacts = 0;
            int successfulAccounts = 0;
            int failedAccounts = 0;

            var domainsToHandle = dbContext.Domains!.Where(d => d.AccountStatus == AccountSyncStatus.NotInitialized);
            var totalSize = domainsToHandle.Count();
            for (var start = 0; start < totalSize; start += batchSize)
            {
                var batch = domainsToHandle.Skip(start).Take(batchSize).ToList();
                totalDomains += batch.Count;
                var (successful, failed) = await SetDomainsAccounts(batch);
                successfulAccounts += successful;
                failedAccounts += failed;
                var domainIdDictionary = batch.ToDictionary(d => d.Id, d => d);
                var contacts = dbContext.Contacts!.Where(c => c.DomainId != null && domainIdDictionary.Keys.Contains(c.DomainId.Value));
                foreach (var c in contacts)
                {
                    c.AccountId = null;
                    c.Account = domainIdDictionary[c.DomainId!.Value].Account;
                    totalContacts++;
                }

                await dbContext.SaveChangesAsync();
            }

            currentJob.Result = $"Processed {totalDomains} domains ({successfulAccounts} successful, {failedAccounts} failed), linked {totalContacts} contacts to accounts";
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error occurred when executing Domain Check task in task runner {currentJob.Id}");
            currentJob.Result = $"Account sync failed: {ex.Message}";
            return false;
        }
    }

    private async Task<(int successfulAccounts, int failedAccounts)> SetDomainsAccounts(List<Domain> domains)
    {
        var newAccounts = new HashSet<Account>();
        int successfulAccounts = 0;
        int failedAccounts = 0;

        foreach (var domain in domains)
        {
            try
            {
                if (domain.Free == true || domain.Disposable == true)
                {
                    domain.AccountStatus = AccountSyncStatus.NotIntended;
                    continue;
                }

                var accInfo = await accountExternalService.GetAccountDetails(domain.Name);
                if (accInfo == null)
                {
                    accInfo = new AccountDetailsInfo() { Name = domain.Name };
                    domain.AccountStatus = AccountSyncStatus.Failed;
                    failedAccounts++;
                }
                else
                {
                    domain.AccountStatus = AccountSyncStatus.Successful;
                    successfulAccounts++;
                }

                var existingAccount = dbContext.Accounts!.FirstOrDefault(a => a.Name == accInfo.Name);
                if (existingAccount == null)
                {
                    existingAccount = newAccounts.FirstOrDefault(a => a.Name == accInfo.Name);
                }

                if (existingAccount != null)
                {
                    domain.Account = existingAccount;
                }
                else
                {
                    var account = mapper.Map<Account>(accInfo);
                    newAccounts.Add(account);
                    domain.Account = account;
                }
            }
            catch (Exception e)
            {
                Log.Error("Cannot set Account for Domain in ContactAccountTask. Domain name: " + domain.Name + ". Reason: " + e.Message);
            }
        }

        await dbContext.Accounts!.AddRangeAsync(newAccounts);
        return (successfulAccounts, failedAccounts);
    }
}