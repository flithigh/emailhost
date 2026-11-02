using EmailHost.Promote.Data;
using EmailHost.Promote.Entities;
using EmailHost.Promote.Security;
using Microsoft.EntityFrameworkCore;

namespace EmailHost.Promote.Services;

public record SignupRequest(
    string OrganizationName,
    string DomainName,
    string AdminEmail,
    string Password,
    string SelectedTier // Starter, Pro, Enterprise
);

public record SignupResponse(
    Guid TenantId,
    string OrganizationName,
    string DomainName,
    string AdminEmail,
    string VerificationToken,
    string ConsoleLoginUrl,
    string WebmailLoginUrl,
    string Message
);

public interface ITenantOnboardingService
{
    Task<SignupResponse> RegisterTenantAsync(SignupRequest request);
}

public class TenantOnboardingService : ITenantOnboardingService
{
    private readonly FlitflapMasterDbContext _masterDb;
    private readonly IPasswordHasher _hasher;

    public TenantOnboardingService(FlitflapMasterDbContext masterDb, IPasswordHasher hasher)
    {
        _masterDb = masterDb;
        _hasher = hasher;
    }

    public async Task<SignupResponse> RegisterTenantAsync(SignupRequest request)
    {
        var cleanDomain = request.DomainName.Trim().ToLowerInvariant();
        var cleanEmail = request.AdminEmail.Trim().ToLowerInvariant();

        if (cleanDomain.StartsWith("http://") || cleanDomain.StartsWith("https://"))
        {
            cleanDomain = new Uri(cleanDomain).Host;
        }

        // Check if domain exists
        var existingDomain = await _masterDb.DomainsRegistry.FirstOrDefaultAsync(d => d.DomainName == cleanDomain);
        if (existingDomain != null)
        {
            throw new InvalidOperationException($"Domain '{cleanDomain}' is already registered on the Flitflap platform.");
        }

        // Check if admin email exists
        var existingUser = await _masterDb.TenantUsers.FirstOrDefaultAsync(u => u.Email == cleanEmail);
        if (existingUser != null)
        {
            throw new InvalidOperationException($"An administrator account with email '{cleanEmail}' already exists.");
        }

        long storageLimit = request.SelectedTier switch
        {
            "Pro" => 250L * 1024L * 1024L * 1024L,
            "Enterprise" => 1024L * 1024L * 1024L * 1024L,
            _ => 50L * 1024L * 1024L * 1024L
        };

        var tenant = new Tenant
        {
            Name = request.OrganizationName.Trim(),
            SubscriptionTier = request.SelectedTier ?? "Starter",
            BillingStatus = "Active",
            ContactEmail = cleanEmail,
            StorageLimitBytes = storageLimit,
            CreatedAtUtc = DateTime.UtcNow
        };

        var verificationToken = $"flitflap-verify-{Guid.NewGuid().ToString("N")[..12]}";

        var domain = new DomainRegistry
        {
            DomainName = cleanDomain,
            DatabaseName = $"domain-database-{cleanDomain.Replace('.', '-')}",
            IsConnected = false,
            IsActive = true,
            VerificationToken = verificationToken,
            CreatedAtUtc = DateTime.UtcNow
        };
        tenant.Domains.Add(domain);

        var user = new TenantUser
        {
            Email = cleanEmail,
            PasswordHash = _hasher.HashPassword(request.Password),
            Role = "Admin",
            CreatedAtUtc = DateTime.UtcNow
        };
        tenant.Users.Add(user);

        _masterDb.Tenants.Add(tenant);

        _masterDb.StaffAuditLogs.Add(new StaffAuditLog
        {
            StaffUser = cleanEmail,
            Action = "PublicSignupCompleted",
            TargetTenantOrDomain = cleanDomain,
            Details = $"Self-service onboarding completed for organization '{tenant.Name}' ({tenant.SubscriptionTier}) with domain '{cleanDomain}'"
        });

        await _masterDb.SaveChangesAsync();

        return new SignupResponse(
            TenantId: tenant.Id,
            OrganizationName: tenant.Name,
            DomainName: cleanDomain,
            AdminEmail: cleanEmail,
            VerificationToken: verificationToken,
            ConsoleLoginUrl: "http://localhost:5000",
            WebmailLoginUrl: "http://localhost:5002",
            Message: "Organization and domain provisioned successfully! You can now verify DNS and provision user mailboxes."
        );
    }
}

