namespace EmailHost.Webmail.Entities;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string SubscriptionTier { get; set; } = "Enterprise"; // Starter, Pro, Enterprise
    public string BillingStatus { get; set; } = "Active";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<DomainRegistry> Domains { get; set; } = new();
    public List<TenantUser> Users { get; set; } = new();
}

public class TenantUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Admin"; // Admin, Support, Billing
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class DomainRegistry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string DomainName { get; set; } = string.Empty; // e.g. client.com
    public string DatabaseName { get; set; } = string.Empty; // e.g. domain-database-XXX
    public string ConnectionString { get; set; } = string.Empty;
    public bool IsConnected { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public string VerificationToken { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedAtUtc { get; set; }
}

public class IpPool
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string IpAddress { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public bool IsWarm { get; set; } = true;
    public double ReputationScore { get; set; } = 100.0;
    public bool IsBlacklisted { get; set; } = false;
}

public class StaffAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StaffUser { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string TargetTenantOrDomain { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

