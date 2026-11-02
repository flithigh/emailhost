using EmailHost.Webmail.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmailHost.Webmail.Data;

public class FlitflapTenantDbContext : DbContext
{
    public FlitflapTenantDbContext(DbContextOptions<FlitflapTenantDbContext> options)
        : base(options)
    {
    }

    public DbSet<TenantMailbox> Mailboxes => Set<TenantMailbox>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<MailAlias> Aliases => Set<MailAlias>();
    public DbSet<DkimKey> DkimKeys => Set<DkimKey>();
    public DbSet<MessageIndex> MessagesIndex => Set<MessageIndex>();
    public DbSet<DomainFilter> DomainFilters => Set<DomainFilter>();
    public DbSet<DeliveryTraceLog> DeliveryTraces => Set<DeliveryTraceLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TenantMailbox>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Address).IsUnique();
            entity.HasIndex(e => e.DomainName);
        });

        modelBuilder.Entity<Contact>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.MailboxId, e.Email });
            entity.HasOne(e => e.Mailbox)
                  .WithMany(m => m.Contacts)
                  .HasForeignKey(e => e.MailboxId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MailAlias>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SourceAlias).IsUnique();
        });

        modelBuilder.Entity<DkimKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.DomainName, e.Selector }).IsUnique();
        });

        modelBuilder.Entity<MessageIndex>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.MailboxId, e.Folder, e.IsDeleted });
            entity.HasIndex(e => e.GarageStorageKey);
            entity.HasOne(e => e.Mailbox)
                  .WithMany(m => m.Messages)
                  .HasForeignKey(e => e.MailboxId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DomainFilter>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Pattern);
        });

        modelBuilder.Entity<DeliveryTraceLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MessageId);
            entity.HasIndex(e => e.TimestampUtc);
        });
    }
}

