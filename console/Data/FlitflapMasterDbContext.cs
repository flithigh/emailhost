using EmailHost.ConsoleApp.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmailHost.ConsoleApp.Data;

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
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.HasMany(e => e.Users).WithOne(u => u.Tenant).HasForeignKey(u => u.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Domains).WithOne(d => d.Tenant).HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<DomainRegistry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DomainName).IsRequired().HasMaxLength(255);
            entity.HasIndex(e => e.DomainName).IsUnique();
        });

        modelBuilder.Entity<IpPool>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IpAddress).IsRequired().HasMaxLength(64);
        });

        modelBuilder.Entity<StaffAuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).HasMaxLength(64);
        });
    }
}

