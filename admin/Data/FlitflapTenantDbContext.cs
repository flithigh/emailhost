using Microsoft.EntityFrameworkCore;

namespace EmailHost.Admin.Data;

public class TenantMailboxSummary
{
    public Guid Id { get; set; }
    public string Address { get; set; } = string.Empty;
    public string DomainName { get; set; } = string.Empty;
    public long QuotaBytes { get; set; }
    public long UsedBytes { get; set; }
    public bool IsActive { get; set; }
}

public class MessageIndexSummary
{
    public Guid Id { get; set; }
    public Guid MailboxId { get; set; }
    public string Folder { get; set; } = "INBOX";
    public long SizeBytes { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
}

public class FlitflapTenantDbContext : DbContext
{
    public FlitflapTenantDbContext(DbContextOptions<FlitflapTenantDbContext> options)
        : base(options)
    {
    }

    public DbSet<TenantMailboxSummary> Mailboxes => Set<TenantMailboxSummary>();
    public DbSet<MessageIndexSummary> MessagesIndex => Set<MessageIndexSummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TenantMailboxSummary>(e =>
        {
            e.ToTable("Mailboxes");
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<MessageIndexSummary>(e =>
        {
            e.ToTable("MessagesIndex");
            e.HasKey(x => x.Id);
        });
    }
}

