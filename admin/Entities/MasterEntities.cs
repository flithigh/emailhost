namespace EmailHost.Admin.Entities;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string SubscriptionTier { get; set; } = "Enterprise"; // Starter, Pro, Enterprise
    public string BillingStatus { get; set; } = "Active"; // Active, Suspended, Terminated
    public string ContactEmail { get; set; } = string.Empty;
    public long StorageLimitBytes { get; set; } = 50L * 1024L * 1024L * 1024L; // 50 GB
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

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

    public string DomainName { get; set; } = string.Empty;
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
    public int WarmupStage { get; set; } = 3; // 1: Initial, 2: Ramping, 3: Fully Warm
    public double ReputationScore { get; set; } = 99.4; // 0.0 - 100.0
    public bool IsBlacklisted { get; set; } = false;
    public string BlacklistDetails { get; set; } = "Clean (Spamhaus, Barracuda, SORBS)";
    public string AssignedTenantName { get; set; } = "Global Shared Pool";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastCheckedAtUtc { get; set; }
}

public class StaffAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StaffUser { get; set; } = "superadmin@flitflap.com";
    public string Action { get; set; } = string.Empty; // e.g. TenantProvisioned, PlanUpgraded, TenantSuspended, IpAdded
    public string TargetTenantOrDomain { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

public class GlobalAbuseRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RuleType { get; set; } = "DomainBlacklist"; // DomainBlacklist, TldBlacklist, IpBlacklist, MaxBounceRateThreshold
    public string Pattern { get; set; } = string.Empty; // e.g. *.spamdomain.com or .click
    public string Reason { get; set; } = "Known phishing or high spam complaint rate";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

