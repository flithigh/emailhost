using EmailHost.Admin.Data;
using EmailHost.Admin.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmailHost.Admin.Services;

public record CreateTenantRequest(string Name, string SubscriptionTier, string ContactEmail, string InitialDomain);

public record TenantDetailDto(
    Guid Id,
    string Name,
    string SubscriptionTier,
    string BillingStatus,
    string ContactEmail,
    long StorageLimitBytes,
    int DomainCount,
    List<string> Domains,
    DateTime CreatedAtUtc
);

public interface ITenantLifecycleService
{
    Task<List<TenantDetailDto>> GetAllTenantsAsync();
    Task<TenantDetailDto?> GetTenantByIdAsync(Guid id);
    Task<Guid> CreateTenantAsync(CreateTenantRequest request, string staffUser = "admin@flitflap.com");
    Task<bool> UpdateSubscriptionTierAsync(Guid id, string newTier, string staffUser = "admin@flitflap.com");
    Task<bool> SuspendTenantAsync(Guid id, string reason, string staffUser = "admin@flitflap.com");
    Task<bool> ReactivateTenantAsync(Guid id, string staffUser = "admin@flitflap.com");
    Task<bool> TerminateTenantAsync(Guid id, string staffUser = "admin@flitflap.com");
}

public class TenantLifecycleService : ITenantLifecycleService
{
    private readonly FlitflapMasterDbContext _masterDb;

    public TenantLifecycleService(FlitflapMasterDbContext masterDb)
    {
        _masterDb = masterDb;
    }

    public async Task<List<TenantDetailDto>> GetAllTenantsAsync()
    {
        var tenants = await _masterDb.Tenants
            .Include(t => t.Domains)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync();

        return tenants.Select(t => new TenantDetailDto(
            t.Id,
            t.Name,
            t.SubscriptionTier,
            t.BillingStatus,
            t.ContactEmail,
            t.StorageLimitBytes,
            t.Domains.Count,
            t.Domains.Select(d => d.DomainName).ToList(),
            t.CreatedAtUtc
        )).ToList();
    }

    public async Task<TenantDetailDto?> GetTenantByIdAsync(Guid id)
    {
        var t = await _masterDb.Tenants
            .Include(t => t.Domains)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (t == null) return null;

        return new TenantDetailDto(
            t.Id,
            t.Name,
            t.SubscriptionTier,
            t.BillingStatus,
            t.ContactEmail,
            t.StorageLimitBytes,
            t.Domains.Count,
            t.Domains.Select(d => d.DomainName).ToList(),
            t.CreatedAtUtc
        );
    }

    public async Task<Guid> CreateTenantAsync(CreateTenantRequest request, string staffUser = "admin@flitflap.com")
    {
        var tenant = new Tenant
        {
            Name = request.Name,
            SubscriptionTier = request.SubscriptionTier,
            ContactEmail = request.ContactEmail,
            BillingStatus = "Active",
            CreatedAtUtc = DateTime.UtcNow
        };

        if (!string.IsNullOrWhiteSpace(request.InitialDomain))
        {
            var cleanDomain = request.InitialDomain.Trim().ToLowerInvariant();
            tenant.Domains.Add(new DomainRegistry
            {
                DomainName = cleanDomain,
                DatabaseName = $"domain-database-{cleanDomain.Replace('.', '-')}",
                VerificationToken = $"flitflap-verify-{Guid.NewGuid().ToString("N")[..12]}",
                IsConnected = true,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        _masterDb.Tenants.Add(tenant);

        _masterDb.StaffAuditLogs.Add(new StaffAuditLog
        {
            StaffUser = staffUser,
            Action = "TenantProvisioned",
            TargetTenantOrDomain = tenant.Name,
            Details = $"Provisioned tenant under tier '{tenant.SubscriptionTier}' with initial domain '{request.InitialDomain}'"
        });

        await _masterDb.SaveChangesAsync();
        return tenant.Id;
    }

    public async Task<bool> UpdateSubscriptionTierAsync(Guid id, string newTier, string staffUser = "admin@flitflap.com")
    {
        var tenant = await _masterDb.Tenants.FindAsync(id);
        if (tenant == null) return false;

        var oldTier = tenant.SubscriptionTier;
        tenant.SubscriptionTier = newTier;
        tenant.UpdatedAtUtc = DateTime.UtcNow;

        if (newTier.Equals("Pro", StringComparison.OrdinalIgnoreCase))
            tenant.StorageLimitBytes = 250L * 1024L * 1024L * 1024L; // 250 GB
        else if (newTier.Equals("Enterprise", StringComparison.OrdinalIgnoreCase))
            tenant.StorageLimitBytes = 1024L * 1024L * 1024L * 1024L; // 1 TB
        else
            tenant.StorageLimitBytes = 50L * 1024L * 1024L * 1024L; // 50 GB

        _masterDb.StaffAuditLogs.Add(new StaffAuditLog
        {
            StaffUser = staffUser,
            Action = "SubscriptionUpgraded",
            TargetTenantOrDomain = tenant.Name,
            Details = $"Updated subscription plan from '{oldTier}' to '{newTier}'. Storage quota adjusted to {tenant.StorageLimitBytes / (1024 * 1024 * 1024)} GB."
        });

        await _masterDb.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SuspendTenantAsync(Guid id, string reason, string staffUser = "admin@flitflap.com")
    {
        var tenant = await _masterDb.Tenants.FindAsync(id);
        if (tenant == null) return false;

        tenant.BillingStatus = "Suspended";
        tenant.UpdatedAtUtc = DateTime.UtcNow;

        _masterDb.StaffAuditLogs.Add(new StaffAuditLog
        {
            StaffUser = staffUser,
            Action = "TenantSuspended",
            TargetTenantOrDomain = tenant.Name,
            Details = $"Tenant suspended by administrator. Reason: {reason}"
        });

        await _masterDb.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ReactivateTenantAsync(Guid id, string staffUser = "admin@flitflap.com")
    {
        var tenant = await _masterDb.Tenants.FindAsync(id);
        if (tenant == null) return false;

        tenant.BillingStatus = "Active";
        tenant.UpdatedAtUtc = DateTime.UtcNow;

        _masterDb.StaffAuditLogs.Add(new StaffAuditLog
        {
            StaffUser = staffUser,
            Action = "TenantReactivated",
            TargetTenantOrDomain = tenant.Name,
            Details = "Tenant returned to Active operational status."
        });

        await _masterDb.SaveChangesAsync();
        return true;
    }

    public async Task<bool> TerminateTenantAsync(Guid id, string staffUser = "admin@flitflap.com")
    {
        var tenant = await _masterDb.Tenants.FindAsync(id);
        if (tenant == null) return false;

        tenant.BillingStatus = "Terminated";
        tenant.UpdatedAtUtc = DateTime.UtcNow;

        _masterDb.StaffAuditLogs.Add(new StaffAuditLog
        {
            StaffUser = staffUser,
            Action = "TenantTerminated",
            TargetTenantOrDomain = tenant.Name,
            Details = "Tenant terminated. Services decommissioned."
        });

        await _masterDb.SaveChangesAsync();
        return true;
    }
}

