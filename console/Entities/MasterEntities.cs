namespace EmailHost.ConsoleApp.Entities;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Active"; // Active, Suspended, Provisioning
    public string SubscriptionTier { get; set; } = "Professional"; // Starter, Professional, Enterprise
    public string BillingStatus { get; set; } = "Paid";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<TenantUser> Users { get; set; } = new();
    public List<DomainRegistry> Domains { get; set; } = new();
}

public class TenantUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Admin";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class DomainRegistry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string DomainName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsConnected { get; set; }
    public string VerificationToken { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string? DatabaseConnectionString { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedAtUtc { get; set; }
}

public class IpPool
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string IpAddress { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int ReputationScore { get; set; } = 100;
    public bool IsWarm { get; set; } = true;
}

public class StaffAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string StaffUsername { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

