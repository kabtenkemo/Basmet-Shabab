using System.Text.Json;
using BasmaApi.Models;
using BasmaApi.Services;
using Microsoft.EntityFrameworkCore;

namespace BasmaApi.Data;

public sealed class AppDbContext : DbContext
{
    private static readonly HashSet<string> AuditedEntityNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Member",
        "MemberTask",
        "Complaint"
    };

    private readonly IAuditRequestContextAccessor _auditRequestContextAccessor;

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : this(options, null)
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options, IAuditRequestContextAccessor? auditRequestContextAccessor)
        : base(options)
    {
        _auditRequestContextAccessor = auditRequestContextAccessor ?? new AuditRequestContextAccessor();
    }

    public DbSet<Member> Members => Set<Member>();

    public DbSet<MemberTask> Tasks => Set<MemberTask>();

    public DbSet<TaskTargetRole> TaskTargetRoles => Set<TaskTargetRole>();

    public DbSet<TaskTargetMember> TaskTargetMembers => Set<TaskTargetMember>();

    public DbSet<MemberPermissionGrant> PermissionGrants => Set<MemberPermissionGrant>();

    public DbSet<PointTransaction> PointTransactions => Set<PointTransaction>();

    public DbSet<Complaint> Complaints => Set<Complaint>();

    public DbSet<ComplaintHistory> ComplaintHistories => Set<ComplaintHistory>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Governorate> Governorates => Set<Governorate>();

    public DbSet<Committee> Committees => Set<Committee>();

    public DbSet<NewsPost> NewsPosts => Set<NewsPost>();

    public DbSet<NewsTargetRole> NewsTargetRoles => Set<NewsTargetRole>();

    public DbSet<NewsTargetMember> NewsTargetMembers => Set<NewsTargetMember>();

    public override int SaveChanges()
    {
        AppendAuditLogs();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AppendAuditLogs();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Member>(entity =>
        {
            entity.HasIndex(member => member.Email).IsUnique();
            entity.Property(member => member.NationalId).HasMaxLength(14);
            entity.Property(member => member.BirthDate).HasColumnType("date");
            entity.Property(member => member.FullName).HasMaxLength(150);
            entity.Property(member => member.Email).HasMaxLength(250);
            entity.Property(member => member.GovernorName).HasMaxLength(120);
            entity.Property(member => member.CommitteeName).HasMaxLength(120);
            entity.Property(member => member.Role).HasConversion<string>().HasMaxLength(40);
            entity.Property(member => member.Points).HasDefaultValue(0);

            entity.HasOne(member => member.CreatedByMember)
                .WithMany()
                .HasForeignKey(member => member.CreatedByMemberId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MemberTask>(entity =>
        {
            entity.Property(task => task.Title).HasMaxLength(200);
            entity.Property(task => task.Description).HasMaxLength(2000);
            entity.Property(task => task.AudienceType).HasConversion<string>().HasMaxLength(20);

            entity.HasOne(task => task.Member)
                .WithMany(member => member.Tasks)
                .HasForeignKey(task => task.MemberId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskTargetRole>(entity =>
        {
            entity.HasIndex(item => new { item.TaskId, item.Role }).IsUnique();
            entity.Property(item => item.Role).HasConversion<string>().HasMaxLength(40);

            entity.HasOne(item => item.Task)
                .WithMany(task => task.TargetRoles)
                .HasForeignKey(item => item.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskTargetMember>(entity =>
        {
            entity.HasIndex(item => new { item.TaskId, item.MemberId }).IsUnique();

            entity.HasOne(item => item.Task)
                .WithMany(task => task.TargetMembers)
                .HasForeignKey(item => item.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(item => item.Member)
                .WithMany()
                .HasForeignKey(item => item.MemberId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MemberPermissionGrant>(entity =>
        {
            entity.Property(grant => grant.PermissionKey).HasMaxLength(120);

            entity.HasOne(grant => grant.Member)
                .WithMany(member => member.PermissionGrants)
                .HasForeignKey(grant => grant.MemberId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(grant => grant.GrantedByMember)
                .WithMany()
                .HasForeignKey(grant => grant.GrantedByMemberId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PointTransaction>(entity =>
        {
            entity.Property(transaction => transaction.Reason).HasMaxLength(500);

            entity.HasOne(transaction => transaction.Member)
                .WithMany(member => member.PointTransactions)
                .HasForeignKey(transaction => transaction.MemberId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(transaction => transaction.RelatedByMember)
                .WithMany()
                .HasForeignKey(transaction => transaction.RelatedByMemberId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Complaint>(entity =>
        {
            entity.Property(complaint => complaint.Subject).HasMaxLength(200);
            entity.Property(complaint => complaint.Message).HasMaxLength(4000);
            entity.Property(complaint => complaint.AdminReply).HasMaxLength(4000);
            entity.Property(complaint => complaint.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(complaint => complaint.Priority).HasConversion<string>().HasMaxLength(20);
            entity.Property(complaint => complaint.LastActionDateUtc).HasColumnName("LastActionDate");

            entity.HasOne(complaint => complaint.Member)
                .WithMany(member => member.Complaints)
                .HasForeignKey(complaint => complaint.MemberId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(complaint => complaint.AssignedToMember)
                .WithMany()
                .HasForeignKey(complaint => complaint.AssignedToMemberId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(complaint => complaint.ReviewedByMember)
                .WithMany()
                .HasForeignKey(complaint => complaint.ReviewedByMemberId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ComplaintHistory>(entity =>
        {
            entity.Property(history => history.Action).HasConversion<string>().HasMaxLength(30);
            entity.Property(history => history.Notes).HasMaxLength(4000);
            entity.Property(history => history.TimestampUtc).HasColumnName("Timestamp");

            entity.HasOne(history => history.Complaint)
                .WithMany(complaint => complaint.Histories)
                .HasForeignKey(history => history.ComplaintId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(history => history.PerformedByUser)
                .WithMany()
                .HasForeignKey(history => history.PerformedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(log => log.UserName).HasMaxLength(150);
            entity.Property(log => log.ActionType).HasMaxLength(50);
            entity.Property(log => log.EntityName).HasMaxLength(80);
            entity.Property(log => log.EntityId).HasMaxLength(100);
            entity.Property(log => log.OldValuesJson).HasColumnType("nvarchar(max)");
            entity.Property(log => log.NewValuesJson).HasColumnType("nvarchar(max)");
            entity.Property(log => log.IPAddress).HasMaxLength(45);
        });

        modelBuilder.Entity<Governorate>(entity =>
        {
            entity.HasIndex(governorate => governorate.Name).IsUnique();
            entity.Property(governorate => governorate.Name).HasMaxLength(120);
        });

        modelBuilder.Entity<Committee>(entity =>
        {
            entity.HasIndex(committee => new { committee.GovernorateId, committee.Name }).IsUnique();
            entity.Property(committee => committee.Name).HasMaxLength(120);
            entity.Property(committee => committee.CreatedAtUtc).HasColumnName("CreatedAtUtc");

            entity.HasOne(committee => committee.Governorate)
                .WithMany(governorate => governorate.Committees)
                .HasForeignKey(committee => committee.GovernorateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NewsPost>(entity =>
        {
            entity.Property(item => item.Title).HasMaxLength(250);
            entity.Property(item => item.Content).HasMaxLength(4000);
            entity.Property(item => item.AudienceType).HasConversion<string>().HasMaxLength(20);

            entity.HasOne(item => item.CreatedByMember)
                .WithMany(member => member.CreatedNewsPosts)
                .HasForeignKey(item => item.CreatedByMemberId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NewsTargetRole>(entity =>
        {
            entity.HasIndex(item => new { item.NewsPostId, item.Role }).IsUnique();
            entity.Property(item => item.Role).HasConversion<string>().HasMaxLength(40);

            entity.HasOne(item => item.NewsPost)
                .WithMany(post => post.TargetRoles)
                .HasForeignKey(item => item.NewsPostId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NewsTargetMember>(entity =>
        {
            entity.HasIndex(item => new { item.NewsPostId, item.MemberId }).IsUnique();

            entity.HasOne(item => item.NewsPost)
                .WithMany(post => post.TargetMembers)
                .HasForeignKey(item => item.NewsPostId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(item => item.Member)
                .WithMany()
                .HasForeignKey(item => item.MemberId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private void AppendAuditLogs()
    {
        var auditEntries = new List<AuditLog>();

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog || entry.State is EntityState.Detached or EntityState.Unchanged)
            {
                continue;
            }

            var entityName = GetAuditEntityName(entry.Entity.GetType().Name);
            if (!AuditedEntityNames.Contains(entityName))
            {
                continue;
            }

            var entityId = entry.Property("Id").CurrentValue?.ToString();
            var actionType = entry.State switch
            {
                EntityState.Added => "Create",
                EntityState.Modified => "Update",
                EntityState.Deleted => "Delete",
                _ => null
            };

            if (actionType is null)
            {
                continue;
            }

            var oldValues = entry.State == EntityState.Modified || entry.State == EntityState.Deleted
                ? CaptureValues(entry, includeCurrentValues: false)
                : null;

            var newValues = entry.State == EntityState.Added || entry.State == EntityState.Modified
                ? CaptureValues(entry, includeCurrentValues: true)
                : null;

            if (entry.State == EntityState.Modified && oldValues is not null && newValues is not null && JsonSerializer.Serialize(oldValues) == JsonSerializer.Serialize(newValues))
            {
                continue;
            }

            var context = _auditRequestContextAccessor.Current;
            auditEntries.Add(new AuditLog
            {
                UserId = context?.UserId,
                UserName = context?.UserName ?? "System",
                ActionType = actionType,
                EntityName = entityName,
                EntityId = entityId,
                OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
                NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues),
                TimestampUtc = DateTime.UtcNow,
                IPAddress = context?.IPAddress
            });
        }

        if (auditEntries.Count > 0)
        {
            AuditLogs.AddRange(auditEntries);
        }
    }

    private static string GetAuditEntityName(string typeName)
    {
        return typeName switch
        {
            nameof(Member) => "User",
            nameof(MemberTask) => "Task",
            nameof(Complaint) => "Complaint",
            _ => typeName
        };
    }

    private static Dictionary<string, object?> CaptureValues(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, bool includeCurrentValues)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in entry.Properties)
        {
            if (property.Metadata.IsPrimaryKey() || string.Equals(property.Metadata.Name, "PasswordHash", StringComparison.OrdinalIgnoreCase) || string.Equals(property.Metadata.Name, "CreatedAtUtc", StringComparison.OrdinalIgnoreCase) || string.Equals(property.Metadata.Name, "UpdatedAtUtc", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            values[property.Metadata.Name] = includeCurrentValues ? property.CurrentValue : property.OriginalValue;
        }

        return values;
    }
}