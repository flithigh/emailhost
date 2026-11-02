using EmailHost.Webmail.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmailHost.Webmail.Data;

public class FlitflapMasterDbContext : DbContext
{
    public FlitflapMasterDbContext(DbContextOptions<FlitflapMasterDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
    public DbSet<DomainRegistry> DomainsRegistry => Set<DomainRegistry>();
    public DbSet<IpPool> IpPools => Set<IpPool>();
    public DbSet<StaffAuditLog> StaffAuditLogs => Set<StaffAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
        });

        modelBuilder.Entity<TenantUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasOne(e => e.Tenant)
                  .WithMany(t => t.Users)
                  .HasForeignKey(e => e.TenantId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DomainRegistry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DomainName).IsUnique();
            entity.HasOne(e => e.Tenant)
                  .WithMany(t => t.Domains)
                  .HasForeignKey(e => e.TenantId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IpPool>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.IpAddress).IsUnique();
        });

        modelBuilder.Entity<StaffAuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TimestampUtc);
        });
    }
}

