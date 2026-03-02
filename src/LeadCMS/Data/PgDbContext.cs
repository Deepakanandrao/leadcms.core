// <copyright file="PgDbContext.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Reflection;
using LeadCMS.DataAnnotations;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Infrastructure;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LeadCMS.Data;

public class PgDbContext : IdentityDbContext<User>
{
    public readonly IConfiguration Configuration;

    private readonly IHttpContextHelper? httpContextHelper;

    /// <summary>
    /// Initializes a new instance of the <see cref="PgDbContext"/> class.
    /// Constructor with no parameters and manual configuration building is required for the case when you would like to use PgDbContext as a base class for a new context (let's say in a plugin).
    /// </summary>
    public PgDbContext()
    {
        try
        {
            Console.WriteLine("Initializing PgDbContext...");

            Configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", false)
                .AddEnvironmentVariables()
                .AddUserSecrets(typeof(Program).Assembly)
                .Build();

            Console.WriteLine("PgDbContext initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to create PgDbContext. Error: {0}, Stack Trace: {1}", ex.Message, ex.StackTrace);
            throw;
        }
    }

    public PgDbContext(DbContextOptions<PgDbContext> options, IConfiguration configuration, IHttpContextHelper httpContextHelper)
        : base(options)
    {
        Configuration = configuration;
        this.httpContextHelper = httpContextHelper;
    }

    public bool IsImportRequest { get; set; }

    public virtual DbSet<Content>? Content { get; set; }

    public virtual DbSet<ContentDraft>? ContentDrafts { get; set; }

    public virtual DbSet<Comment>? Comments { get; set; }

    public virtual DbSet<Contact>? Contacts { get; set; }

    public virtual DbSet<Order>? Orders { get; set; }

    public virtual DbSet<OrderItem>? OrderItems { get; set; }

    public virtual DbSet<TaskExecutionLog>? TaskExecutionLogs { get; set; }

    public virtual DbSet<Media>? Media { get; set; }

    public virtual DbSet<Entities.File>? Files { get; set; }

    public virtual DbSet<EmailGroup>? EmailGroups { get; set; }

    public virtual DbSet<EmailSchedule>? EmailSchedules { get; set; }

    public virtual DbSet<EmailTemplate>? EmailTemplates { get; set; }

    public virtual DbSet<ContactEmailSchedule>? ContactEmailSchedules { get; set; }

    public virtual DbSet<EmailLog>? EmailLogs { get; set; }

    public virtual DbSet<IpDetails>? IpDetails { get; set; }

    public virtual DbSet<ChangeLog>? ChangeLogs { get; set; }

    public virtual DbSet<ChangeLogTaskLog>? ChangeLogTaskLogs { get; set; }

    public virtual DbSet<Link>? Links { get; set; }

    public virtual DbSet<LinkLog>? LinkLogs { get; set; }

    public virtual DbSet<Domain>? Domains { get; set; }

    public virtual DbSet<Account>? Accounts { get; set; }

    public virtual DbSet<Unsubscribe>? Unsubscribes { get; set; }

    public virtual DbSet<Deal>? Deals { get; set; }

    public virtual DbSet<DealPipeline>? DealPipelines { get; set; }

    public virtual DbSet<DealPipelineStage>? DealPipelineStages { get; set; }

    public virtual DbSet<Promotion>? Promotions { get; set; }

    public virtual DbSet<Discount>? Discounts { get; set; }

    public virtual DbSet<MailServer>? MailServers { get; set; }

    public virtual DbSet<ContentType>? ContentTypes { get; set; }

    public virtual DbSet<Setting>? Settings { get; set; }

    public virtual DbSet<Segment>? Segments { get; set; }

    public virtual DbSet<EnrichmentProviderConfig>? EnrichmentProviderConfigs { get; set; }

    public virtual DbSet<EnrichmentWorkItem>? EnrichmentWorkItems { get; set; }

    public virtual DbSet<EnrichmentProviderAttempt>? EnrichmentProviderAttempts { get; set; }

    public virtual DbSet<EnrichmentAudit>? EnrichmentAudits { get; set; }

    public virtual DbSet<EnrichmentQuotaUsage>? EnrichmentQuotaUsages { get; set; }

    public virtual DbSet<Campaign>? Campaigns { get; set; }

    public virtual DbSet<CampaignRecipient>? CampaignRecipients { get; set; }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var result = 0;
        var changes = new Dictionary<EntityEntry, ChangeLog>();

        var entries = ChangeTracker
           .Entries()
           .Where(e => e.Entity is BaseEntityWithId && (
                   e.State == EntityState.Added
                   || e.State == EntityState.Modified
                   || e.State == EntityState.Deleted));

        var currentUserId = await httpContextHelper!.GetCurrentUserIdAsync();
        var userIpAddress = httpContextHelper!.IpAddressV4;
        var userAgent = httpContextHelper!.UserAgent;

        if (entries.Any())
        {
            foreach (var entityEntry in entries)
            {
                if (entityEntry.State == EntityState.Added)
                {
                    var createdAtEntity = entityEntry.Entity as IHasCreatedAt;

                    if (createdAtEntity is not null)
                    {
                        createdAtEntity.CreatedAt = createdAtEntity.CreatedAt == DateTime.MinValue ? DateTime.UtcNow : GetDateWithKind(createdAtEntity.CreatedAt);
                    }

                    var createdByEntity = entityEntry.Entity as IHasCreatedBy;

                    if (createdByEntity is not null)
                    {
                        createdByEntity.CreatedById = currentUserId;
                        createdByEntity.CreatedByIp = string.IsNullOrEmpty(createdByEntity.CreatedByIp) ? userIpAddress : createdByEntity.CreatedByIp;
                        createdByEntity.CreatedByUserAgent = string.IsNullOrEmpty(createdByEntity.CreatedByUserAgent) ? userAgent : createdByEntity.CreatedByUserAgent;
                    }
                }

                if (entityEntry.State == EntityState.Modified)
                {
                    var updatedAtEntity = entityEntry.Entity as IHasUpdatedAt;

                    if (updatedAtEntity is not null)
                    {
                        updatedAtEntity.UpdatedAt = IsImportRequest && updatedAtEntity.UpdatedAt is not null ? GetDateWithKind(updatedAtEntity.UpdatedAt.Value) : DateTime.UtcNow;
                    }

                    var updatedByEntity = entityEntry.Entity as IHasUpdatedBy;

                    if (updatedByEntity is not null)
                    {
                        updatedByEntity.UpdatedById = currentUserId;
                        updatedByEntity.UpdatedByIp = IsImportRequest && !string.IsNullOrEmpty(updatedByEntity.UpdatedByIp) ? updatedByEntity.UpdatedByIp : userIpAddress;
                        updatedByEntity.UpdatedByUserAgent = IsImportRequest && !string.IsNullOrEmpty(updatedByEntity.UpdatedByUserAgent) ? updatedByEntity.UpdatedByUserAgent : userAgent;
                    }
                }

                var entityType = entityEntry.Entity.GetType();

                if (entityType!.GetCustomAttributes<SupportsChangeLogAttribute>().Any())
                {
                    // save entity state as it is before SaveChanges call
                    changes[entityEntry] = new ChangeLog
                    {
                        ObjectType = entityEntry.Entity.GetType().Name,
                        EntityState = entityEntry.State,
                        CreatedAt = DateTime.UtcNow,
                    };
                }
            }
        }

        if (changes.Count > 0)
        {
            // save original records and obtain ids (to preserve ids in change_log)
            result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

            foreach (var change in changes)
            {
                // save object id which we only recieve after SaveChanges (for new records)
                change.Value.ObjectId = ((BaseEntityWithId)change.Key.Entity).Id;
                change.Value.Data = JsonHelper.Serialize(change.Key.Entity);
            }

            ChangeLogs!.AddRange(changes.Values);

            // Save the change log entries
            result += await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

            // Send PostgreSQL NOTIFY for entity changes (only if there were actual changes)
            if (changes.Any())
            {
                try
                {
                    Console.WriteLine($"Sending NOTIFY entity_changes for {changes.Count} changes");
                    await Database.ExecuteSqlRawAsync("NOTIFY entity_changes;", cancellationToken);
                    Console.WriteLine("NOTIFY entity_changes sent successfully");
                }
                catch (Exception ex)
                {
                    // Log the error but don't fail the transaction
                    Console.WriteLine($"Failed to send NOTIFY: {ex.Message}");
                }
            }
        }
        else
        {
            result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        return result;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        try
        {
            Console.WriteLine("Configuring PgDbContext...");

            optionsBuilder.UseNpgsql(
                DataSourceSingleton.GetInstance(Configuration),
                b => b.MigrationsHistoryTable("_migrations"))
                        .UseSnakeCaseNamingConvention()
                        .ReplaceService<IMigrationsSqlGenerator, CustomSqlServerMigrationsSqlGenerator>();

            Console.WriteLine("PgDbContext successfully configured");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to configure PgDbContext. Error: {0}, Stack Trace: {1}", ex.Message, ex.StackTrace);
            throw;
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Override default AspNet Identity table names
        builder.Entity<User>(entity => { entity.ToTable(name: "users"); });
        builder.Entity<IdentityRole>(entity => { entity.ToTable(name: "roles"); });
        builder.Entity<IdentityUserRole<string>>(entity => { entity.ToTable("user_roles"); });
        builder.Entity<IdentityUserClaim<string>>(entity => { entity.ToTable("user_claims"); });
        builder.Entity<IdentityUserLogin<string>>(entity => { entity.ToTable("user_logins"); });
        builder.Entity<IdentityUserToken<string>>(entity => { entity.ToTable("user_tokens"); });
        builder.Entity<IdentityRoleClaim<string>>(entity => { entity.ToTable("role_claims"); });

        builder.Entity<User>().Property(u => u.Data).HasColumnType("jsonb");

        // Add unique index on NormalizedEmail to prevent duplicate emails
        builder.Entity<User>().HasIndex(u => u.NormalizedEmail).IsUnique();

        // Contact: partial unique index on Email (only when non-null)
        builder.Entity<Contact>()
            .HasIndex(c => c.Email)
            .IsUnique()
            .HasFilter("\"email\" IS NOT NULL");

        // Contact: partial index on Phone (only when non-null, E.164 normalized)
        builder.Entity<Contact>()
            .HasIndex(c => c.Phone)
            .HasFilter("\"phone\" IS NOT NULL");

        // Contact: partial index on PhoneRaw (only when non-null)
        builder.Entity<Contact>()
            .HasIndex(c => c.PhoneRaw)
            .HasFilter("\"phone_raw\" IS NOT NULL");

        // Contact: PendingUpdates stored as JSONB
        builder.Entity<Contact>()
            .Property(c => c.PendingUpdates)
            .HasColumnType("jsonb");

        // Configure Contact FullName as computed column
        // Use CASE statements for conditional concatenation - all functions used are IMMUTABLE
        builder.Entity<Contact>()
            .Property(c => c.FullName)
            .HasComputedColumnSql(
                "TRIM(COALESCE(\"first_name\", '') || " +
                "CASE WHEN COALESCE(\"middle_name\", '') != '' THEN ' ' || \"middle_name\" ELSE '' END || " +
                "CASE WHEN COALESCE(\"last_name\", '') != '' THEN ' ' || \"last_name\" ELSE '' END)",
                stored: true);

        // Fix ContentType foreign key for Content
        builder.Entity<Content>()
            .HasOne(c => c.ContentType)
            .WithMany()
            .HasForeignKey(c => c.Type)
            .HasPrincipalKey(ct => ct.Uid);

        builder.Entity<EnrichmentWorkItem>()
            .HasOne(w => w.ProviderConfig)
            .WithMany(p => p.WorkItems)
            .HasForeignKey(w => w.ProviderKey)
            .HasPrincipalKey(p => p.ProviderKey)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<EnrichmentProviderAttempt>()
            .HasOne(a => a.WorkItem)
            .WithMany(w => w.Attempts)
            .HasForeignKey(a => a.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // Set CampaignId to null when a campaign is deleted
        builder.Entity<EmailLog>()
            .HasOne(e => e.Campaign)
            .WithMany()
            .HasForeignKey(e => e.CampaignId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Deal>()
            .HasOne(d => d.Campaign)
            .WithMany()
            .HasForeignKey(d => d.CampaignId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Order>()
            .HasOne(o => o.Campaign)
            .WithMany()
            .HasForeignKey(o => o.CampaignId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private DateTime GetDateWithKind(DateTime date)
    {
        if (date.Kind == DateTimeKind.Unspecified /*|| date.Kind == DateTimeKind.Local*/)
        {
            return DateTime.SpecifyKind(date, DateTimeKind.Utc);
        }

        return date;
    }
}