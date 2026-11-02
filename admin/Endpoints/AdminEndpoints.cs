using EmailHost.Admin.Services;
using Microsoft.AspNetCore.Mvc;

namespace EmailHost.Admin.Endpoints;

public record UpgradeTierRequest(string Tier);
public record SuspendTenantRequest(string Reason);
public record WarmupRequest(int Stage);

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin");

        // Overview Summary
        group.MapGet("/overview", async (
            ITenantLifecycleService tenantService,
            IInfrastructureMonitorService infraService,
            IIpPoolGovernanceService ipService) =>
        {
            var tenants = await tenantService.GetAllTenantsAsync();
            var infra = await infraService.GetClusterHealthAsync();
            var ips = await ipService.GetAllIpsAsync();

            double avgReputation = ips.Count > 0 ? ips.Average(i => i.ReputationScore) : 100.0;
            int blacklistedIps = ips.Count(i => i.IsBlacklisted);

            return Results.Ok(new
            {
                totalTenants = tenants.Count,
                activeTenants = tenants.Count(t => t.BillingStatus == "Active"),
                suspendedTenants = tenants.Count(t => t.BillingStatus == "Suspended"),
                totalDomains = infra.TotalDomains,
                totalMailboxes = infra.TotalMailboxes,
                totalMessages = infra.TotalMessages,
                garageS3Healthy = infra.GarageS3Healthy,
                masterDbHealthy = infra.MasterDbHealthy,
                smtpEngineHealthy = infra.SmtpEngineHealthy,
                smtpEngineStatus = infra.SmtpEngineStatus,
                avgIpReputation = Math.Round(avgReputation, 1),
                blacklistedIps,
                systemWideSizeBytes = infra.SystemWideSizeBytes
            });
        });

        // 1. Tenant Lifecycle Endpoints
        group.MapGet("/tenants", async (ITenantLifecycleService service) =>
        {
            var tenants = await service.GetAllTenantsAsync();
            return Results.Ok(tenants);
        });

        group.MapGet("/tenants/{id:guid}", async (Guid id, ITenantLifecycleService service) =>
        {
            var tenant = await service.GetTenantByIdAsync(id);
            return tenant != null ? Results.Ok(tenant) : Results.NotFound();
        });

        group.MapPost("/tenants", async ([FromBody] CreateTenantRequest req, ITenantLifecycleService service) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { message = "Tenant organization name is required." });

            var id = await service.CreateTenantAsync(req);
            return Results.Created($"/api/admin/tenants/{id}", new { id, message = "Tenant provisioned successfully." });
        });

        group.MapPost("/tenants/{id:guid}/upgrade", async (Guid id, [FromBody] UpgradeTierRequest req, ITenantLifecycleService service) =>
        {
            var success = await service.UpdateSubscriptionTierAsync(id, req.Tier);
            return success ? Results.Ok(new { message = "Subscription tier upgraded." }) : Results.NotFound();
        });

        group.MapPost("/tenants/{id:guid}/suspend", async (Guid id, [FromBody] SuspendTenantRequest req, ITenantLifecycleService service) =>
        {
            var success = await service.SuspendTenantAsync(id, req.Reason ?? "Violation of Acceptable Use Policy");
            return success ? Results.Ok(new { message = "Tenant account suspended." }) : Results.NotFound();
        });

        group.MapPost("/tenants/{id:guid}/reactivate", async (Guid id, ITenantLifecycleService service) =>
        {
            var success = await service.ReactivateTenantAsync(id);
            return success ? Results.Ok(new { message = "Tenant reactivated to active status." }) : Results.NotFound();
        });

        group.MapPost("/tenants/{id:guid}/terminate", async (Guid id, ITenantLifecycleService service) =>
        {
            var success = await service.TerminateTenantAsync(id);
            return success ? Results.Ok(new { message = "Tenant account terminated." }) : Results.NotFound();
        });

        // 2. Infrastructure Telemetry
        group.MapGet("/infra", async (IInfrastructureMonitorService service) =>
        {
            var health = await service.GetClusterHealthAsync();
            return Results.Ok(health);
        });

        // 3. IP Pool & Relay Governance
        group.MapGet("/ip-pools", async (IIpPoolGovernanceService service) =>
        {
            var ips = await service.GetAllIpsAsync();
            return Results.Ok(ips);
        });

        group.MapPost("/ip-pools", async ([FromBody] AddIpRequest req, IIpPoolGovernanceService service) =>
        {
            if (string.IsNullOrWhiteSpace(req.IpAddress))
                return Results.BadRequest(new { message = "IP Address is required." });

            var ip = await service.AddIpAsync(req);
            return Results.Created($"/api/admin/ip-pools/{ip.Id}", ip);
        });

        group.MapPost("/ip-pools/{id:guid}/warmup", async (Guid id, [FromBody] WarmupRequest req, IIpPoolGovernanceService service) =>
        {
            var success = await service.ToggleWarmupAsync(id, req.Stage);
            return success ? Results.Ok(new { message = "IP warmup stage updated." }) : Results.NotFound();
        });

        group.MapPost("/ip-pools/{id:guid}/check", async (Guid id, IIpPoolGovernanceService service) =>
        {
            var result = await service.CheckBlacklistAsync(id);
            return result != null ? Results.Ok(result) : Results.NotFound();
        });

        group.MapPost("/ip-pools/check-all", async (IIpPoolGovernanceService service) =>
        {
            await service.RunAllBlacklistAuditsAsync();
            return Results.Ok(new { message = "Full DNS blacklist audit completed across all outbound IPs." });
        });

        // 4. Global Security & Abuse Control
        group.MapGet("/security/rules", async (ISecurityAbuseService service) =>
        {
            var rules = await service.GetAbuseRulesAsync();
            return Results.Ok(rules);
        });

        group.MapPost("/security/rules", async ([FromBody] AddAbuseRuleRequest req, ISecurityAbuseService service) =>
        {
            if (string.IsNullOrWhiteSpace(req.Pattern))
                return Results.BadRequest(new { message = "Pattern rule is required." });

            var rule = await service.AddAbuseRuleAsync(req);
            return Results.Created($"/api/admin/security/rules/{rule.Id}", rule);
        });

        group.MapDelete("/security/rules/{id:guid}", async (Guid id, ISecurityAbuseService service) =>
        {
            var success = await service.DeleteAbuseRuleAsync(id);
            return success ? Results.Ok() : Results.NotFound();
        });

        // 5. Audit Trail
        group.MapGet("/audit-logs", async ([FromQuery] int limit, ISecurityAbuseService service) =>
        {
            var logs = await service.GetAuditLogsAsync(limit > 0 ? limit : 50);
            return Results.Ok(logs);
        });
    }
}

