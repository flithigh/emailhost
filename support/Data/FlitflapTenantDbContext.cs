using EmailHost.Support.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmailHost.Support.Data;

public class FlitflapTenantDbContext : DbContext
{
    public FlitflapTenantDbContext(DbContextOptions<FlitflapTenantDbContext> options)
        : base(options)
    {
    }

    public DbSet<TenantMailbox> Mailboxes => Set<TenantMailbox>();
    public DbSet<MessageIndex> MessagesIndex => Set<MessageIndex>();
    public DbSet<DeliveryTraceLog> DeliveryTraces => Set<DeliveryTraceLog>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TenantMailbox>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Address);
            entity.HasIndex(e => e.DomainName);
        });

        modelBuilder.Entity<MessageIndex>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MailboxId);
            entity.HasIndex(e => e.Sender);
            entity.HasIndex(e => e.Recipient);
        });

        modelBuilder.Entity<DeliveryTraceLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MessageId);
            entity.HasIndex(e => e.TimestampUtc);
        });

        modelBuilder.Entity<SupportTicket>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TicketNumber).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.DomainName);
        });
    }
}

