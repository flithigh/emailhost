using EmailHost.Smtp.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmailHost.Smtp.Data;

public class SmtpDbContext : DbContext
{
    public SmtpDbContext(DbContextOptions<SmtpDbContext> options)
        : base(options)
    {
    }

    public DbSet<Domain> Domains => Set<Domain>();
    public DbSet<Mailbox> Mailboxes => Set<Mailbox>();
    public DbSet<DomainSmtpConfig> SmtpConfigs => Set<DomainSmtpConfig>();
    public DbSet<EmailMessage> Messages => Set<EmailMessage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Domain>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.HasIndex(e => e.Name).IsUnique();

            entity.HasMany(e => e.Mailboxes)
                  .WithOne(m => m.Domain)
                  .HasForeignKey(m => m.DomainId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.SmtpConfig)
                  .WithOne(s => s.Domain)
                  .HasForeignKey<DomainSmtpConfig>(s => s.DomainId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Mailbox>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EmailAddress).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.EmailAddress).IsUnique();

            entity.HasMany(e => e.Messages)
                  .WithOne(m => m.Mailbox)
                  .HasForeignKey(m => m.MailboxId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmailMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MessageId).HasMaxLength(255);
            entity.Property(e => e.From).HasMaxLength(256);
            entity.Property(e => e.To).HasMaxLength(256);
            entity.Property(e => e.Folder).HasMaxLength(64);
            entity.HasIndex(e => new { e.MailboxId, e.Folder });
        });

        modelBuilder.Entity<DomainSmtpConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DomainId).IsUnique();
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClientIp).HasMaxLength(64);
            entity.Property(e => e.Action).HasMaxLength(64);
        });
    }
}

