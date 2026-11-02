using EmailHost.Admin.Data;
using EmailHost.Admin.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmailHost.Admin.Services;

public record AddAbuseRuleRequest(string RuleType, string Pattern, string Reason);

public interface ISecurityAbuseService
{
    Task<List<GlobalAbuseRule>> GetAbuseRulesAsync();
    Task<GlobalAbuseRule> AddAbuseRuleAsync(AddAbuseRuleRequest request, string staffUser = "admin@flitflap.com");
    Task<bool> DeleteAbuseRuleAsync(Guid id, string staffUser = "admin@flitflap.com");
    Task<List<StaffAuditLog>> GetAuditLogsAsync(int limit = 50);
}

public class SecurityAbuseService : ISecurityAbuseService
{
    private readonly FlitflapMasterDbContext _masterDb;

    public SecurityAbuseService(FlitflapMasterDbContext masterDb)
    {
        _masterDb = masterDb;
    }

    public async Task<List<GlobalAbuseRule>> GetAbuseRulesAsync()
    {
        return await _masterDb.GlobalAbuseRules
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<GlobalAbuseRule> AddAbuseRuleAsync(AddAbuseRuleRequest request, string staffUser = "admin@flitflap.com")
    {
        var rule = new GlobalAbuseRule
        {
            RuleType = request.RuleType,
            Pattern = request.Pattern.Trim().ToLowerInvariant(),
            Reason = request.Reason.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _masterDb.GlobalAbuseRules.Add(rule);
        _masterDb.StaffAuditLogs.Add(new StaffAuditLog
        {
            StaffUser = staffUser,
            Action = "GlobalAbuseRuleAdded",
            TargetTenantOrDomain = rule.Pattern,
            Details = $"Added {rule.RuleType} for pattern '{rule.Pattern}'. Reason: {rule.Reason}"
        });

        await _masterDb.SaveChangesAsync();
        return rule;
    }

    public async Task<bool> DeleteAbuseRuleAsync(Guid id, string staffUser = "admin@flitflap.com")
    {
        var rule = await _masterDb.GlobalAbuseRules.FindAsync(id);
        if (rule == null) return false;

        _masterDb.GlobalAbuseRules.Remove(rule);
        _masterDb.StaffAuditLogs.Add(new StaffAuditLog
        {
            StaffUser = staffUser,
            Action = "GlobalAbuseRuleDeleted",
            TargetTenantOrDomain = rule.Pattern,
            Details = $"Removed {rule.RuleType} rule for pattern '{rule.Pattern}'"
        });

        await _masterDb.SaveChangesAsync();
        return true;
    }

    public async Task<List<StaffAuditLog>> GetAuditLogsAsync(int limit = 50)
    {
        return await _masterDb.StaffAuditLogs
            .OrderByDescending(l => l.TimestampUtc)
            .Take(limit)
            .ToListAsync();
    }
}

