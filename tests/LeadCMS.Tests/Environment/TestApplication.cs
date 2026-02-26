// <copyright file="TestApplication.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Core.AIAssistance.Interfaces;
using LeadCMS.Data;
using LeadCMS.Plugin.TestPlugin.Data;
using LeadCMS.Tests.TestServices;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nest;

namespace LeadCMS.Tests.Environment;

public class TestApplication : WebApplicationFactory<Program>
{
    private static readonly object LockObject = new object();

    private static bool databaseInitialized = false;

    public TestApplication()
    {
        var projectDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(projectDir, "appsettings.tests.json");

        Program.AddAppSettingsJsonFile(configPath);

        // Initialize database once on first TestApplication instance
        EnsureDatabaseInitialized();
    }

    public void EnsureDatabaseInitialized()
    {
        if (databaseInitialized)
        {
            return;
        }

        lock (LockObject)
        {
            // Double-check after acquiring lock
            if (databaseInitialized)
            {
                return;
            }

            using (var scope = Services.CreateScope())
            {
                var dataContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();

                // Initialize database schema and seed data
                RenewDatabase(dataContext);

                // Create default identity (admin user, roles, etc.)
                Program.CreateDefaultIdentity(scope).Wait();

                databaseInitialized = true;
            }
        }
    }

    public void CleanDatabase(HashSet<Type>? usedEntityTypes = null)
    {
        lock (LockObject)
        {
            using (var scope = Services.CreateScope())
            {
                var dataContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();

                // Only truncate tables - no need to recreate schema
                TruncateTables(dataContext, usedEntityTypes);

                // ElasticSearch cleanup if needed
                var esDbContext = scope.ServiceProvider.GetRequiredService<EsDbContext>();
                if (esDbContext.ElasticClient != null)
                {
                    esDbContext.ElasticClient.Indices.Delete("*");
                }
            }
        }
    }

    public void ResetDatabase()
    {
        lock (LockObject)
        {
            using (var scope = Services.CreateScope())
            {
                var dataContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();

                // Full database reset - recreate schema and seed data
                RenewDatabase(dataContext);
                Program.CreateDefaultIdentity(scope).Wait();
            }
        }
    }

    public ElasticClient GetElasticClient()
    {
        using (var scope = Services.CreateScope())
        {
            var esDbContext = scope.ServiceProvider.GetRequiredService<EsDbContext>();
            return esDbContext.ElasticClient;
        }
    }

    public void PopulateBulkData<T, TS>(dynamic bulkItems)
        where T : BaseEntityWithId
        where TS : IEntityService<T>
    {
        using (var scope = Services.CreateScope())
        {
            var dataContaxt = scope.ServiceProvider.GetRequiredService<PgDbContext>();

            var saveService = scope.ServiceProvider.GetService<TS>();

            if (saveService != null)
            {
                saveService.SaveRangeAsync(bulkItems).Wait();
            }
            else
            {
                dataContaxt.AddRange(bulkItems);
            }

            dataContaxt.SaveChangesAsync().Wait();
        }
    }

    public PgDbContext? GetDbContext()
    {
        var scope = Services.CreateScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();
        return dataContext;
    }

    public IMapper GetMapper()
    {
        using (var serviceScope = Services.CreateScope())
        {
            return serviceScope.ServiceProvider.GetService<IMapper>()!;
        }
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureServices((context, services) =>
        {
            services.AddScoped<TestPluginDbContext, TestPluginDbContext>();

            services.AddScoped<IEmailService, TestEmailService>();
            services.AddScoped<IEmailValidationExternalService, TestEmailValidationExternalService>();
            services.AddScoped<IAccountExternalService, TestAccountExternalService>();
            services.AddSingleton<IAIProviderService, TestAIProviderService>();

            // Register a test settings provider to validate the ISettingsProvider pipeline
            services.AddSingleton<ISettingsProvider>(new TestPluginSettingsProvider());
        });

        return base.CreateHost(builder);
    }

    private void TruncateTables(PgDbContext context, HashSet<Type>? usedEntityTypes = null)
    {
        try
        {
            // Get table names - either from used entity types or all tables
            var tablesToClean = usedEntityTypes != null && usedEntityTypes.Any()
                ? GetTablesToCleanForEntityTypes(context, usedEntityTypes)
                : GetTablesToClean(context);

            if (!tablesToClean.Any())
            {
                return; // Nothing to clean
            }

            // Disable triggers and constraints temporarily for faster truncation
            context.Database.ExecuteSqlRaw("SET session_replication_role = 'replica';");

            foreach (var table in tablesToClean)
            {
                try
                {
                    // Table names are from DbContext model metadata, not user input
                    // RESTART IDENTITY resets auto-increment sequences
#pragma warning disable EF1002
                    context.Database.ExecuteSqlRaw($"TRUNCATE TABLE \"{table}\" RESTART IDENTITY CASCADE;");
#pragma warning restore EF1002
                }
                catch
                {
                    // Table might not exist or have issues, continue
                }
            }

            // Re-enable triggers and constraints
            context.Database.ExecuteSqlRaw("SET session_replication_role = 'origin';");
        }
        catch (Exception ex)
        {
            // If truncate fails, fall back to full database renewal
            Console.WriteLine($"Truncate failed: {ex.Message}. Falling back to full database renewal.");
            RenewDatabase(context);
        }
    }

    private List<string> GetTablesToClean(PgDbContext context)
    {
        var tables = new List<string>();
        var entityTypes = context.Model.GetEntityTypes();

        foreach (var entityType in entityTypes)
        {
            var tableName = entityType.GetTableName();
            if (!string.IsNullOrEmpty(tableName))
            {
                tables.Add(tableName);
            }
        }

        return tables;
    }

    private List<string> GetTablesToCleanForEntityTypes(PgDbContext context, HashSet<Type> usedEntityTypes)
    {
        var tables = new HashSet<string>();
        var entityTypes = context.Model.GetEntityTypes();

        foreach (var entityType in entityTypes)
        {
            // Check if this entity type or any of its derived types were used
            var clrType = entityType.ClrType;
            if (usedEntityTypes.Contains(clrType) || usedEntityTypes.Any(t => clrType.IsAssignableFrom(t)))
            {
                var tableName = entityType.GetTableName();
                if (!string.IsNullOrEmpty(tableName))
                {
                    tables.Add(tableName);
                }
            }
        }

        return tables.ToList();
    }

    private void RenewDatabase(PgDbContext context)
    {
        try
        {
            context.Database.EnsureDeleted();
            context.Database.Migrate();
        }
        catch
        {
            Thread.Sleep(1000);
            RenewDatabase(context);
        }
    }
}