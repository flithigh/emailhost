using EmailHost.Smtp.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmailHost.Smtp.Data;

public class FlitflapTenantDbContext : DbContext
{
    public FlitflapTenantDbContext(DbContextOptions<FlitflapTenantDbContext> options)
        : base(options)
    {
    }

    public DbSet<TenantMailbox> Mailboxes => Set<TenantMailbox>();
    public DbSet<MailAlias> Aliases => Set<MailAlias>();
    public DbSet<DkimKey> DkimKeys => Set<DkimKey>();
    public DbSet<MessageIndex> MessagesIndex => Set<MessageIndex>();
    public DbSet<DomainFilter> DomainFilters => Set<DomainFilter>();
    public DbSet<DeliveryTraceLog> DeliveryTraceLogs => Set<DeliveryTraceLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TenantMailbox>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Address).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.Address).IsUnique();
            entity.HasMany(e => e.Messages).WithOne(m => m.Mailbox).HasForeignKey(m => m.MailboxId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MailAlias>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SourceAlias).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.SourceAlias);
        });

        modelBuilder.Entity<DkimKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DomainName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Selector).IsRequired().HasMaxLength(64);
            entity.HasIndex(e => new { e.DomainName, e.Selector });
        });

        modelBuilder.Entity<MessageIndex>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Folder).IsRequired().HasMaxLength(64);
            entity.Property(e => e.GarageStorageKey).IsRequired().HasMaxLength(512);
            entity.HasIndex(e => new { e.MailboxId, e.Folder });
            entity.HasIndex(e => e.GarageStorageKey);
        });

        modelBuilder.Entity<DomainFilter>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Pattern).IsRequired().HasMaxLength(256);
        });

        modelBuilder.Entity<DeliveryTraceLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MessageId).HasMaxLength(255);
            entity.HasIndex(e => e.MessageId);
        });
    }
}

