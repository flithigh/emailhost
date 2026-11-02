namespace EmailHost.Support.Entities;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string SubscriptionTier { get; set; } = "Enterprise"; // Starter, Pro, Enterprise
    public string BillingStatus { get; set; } = "Active"; // Active, Suspended, Terminated
    public string ContactEmail { get; set; } = string.Empty;
    public long StorageLimitBytes { get; set; } = 50L * 1024L * 1024L * 1024L;
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
    public string Role { get; set; } = "Admin";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class DomainRegistry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string DomainName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public bool IsConnected { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public string VerificationToken { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedAtUtc { get; set; }
}

public class StaffAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StaffUser { get; set; } = "support@flitflap.com";
    public string Action { get; set; } = string.Empty; // e.g. MailTraceInspected, DnsDiagnosed, QuotaAdjusted, TicketResolved
    public string TargetTenantOrDomain { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

