using EmailHost.Support.Data;
using EmailHost.Support.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmailHost.Support.Endpoints;

public record AdjustQuotaPayload(string Address, long AdditionalBytes, string Reason);

public static class SupportEndpoints
{
    public static void MapSupportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/support");

        // Overview
        group.MapGet("/overview", async (
            FlitflapTenantDbContext tenantDb,
            FlitflapMasterDbContext masterDb) =>
        {
            int openTickets = await tenantDb.SupportTickets.CountAsync(t => t.Status == "Open" || t.Status == "In Progress");
            int totalTraces = await tenantDb.DeliveryTraces.CountAsync();
            int tenantsCount = await masterDb.Tenants.CountAsync();
            int domainsCount = await masterDb.DomainsRegistry.CountAsync();

            return Results.Ok(new
            {
                openTickets,
                totalTraces,
                tenantsCount,
                domainsCount
            });
        });

        // 1. Mail Tracer Endpoints
        group.MapGet("/tracer", async ([FromQuery] string? query, IMailTracerDiagnosticService service) =>
        {
            var traces = await service.GetRecentTracesAsync(query, 50);
            return Results.Ok(traces);
        });

        group.MapGet("/tracer/{messageId}", async (string messageId, IMailTracerDiagnosticService service) =>
        {
            var detail = await service.GetTraceDetailAsync(messageId);
            return detail != null ? Results.Ok(detail) : Results.NotFound();
        });

        // 2. Tenant Lookup & DNS Diagnostics
        group.MapGet("/tenants", async ([FromQuery] string? q, ITenantDiagnosticService service) =>
        {
            var list = await service.SearchTenantsAsync(q ?? "");
            return Results.Ok(list);
        });

        group.MapGet("/dns/diagnose", async ([FromQuery] string domain, ITenantDiagnosticService service) =>
        {
            if (string.IsNullOrWhiteSpace(domain))
                return Results.BadRequest(new { message = "Domain name is required." });

            var result = await service.DiagnoseDomainDnsAsync(domain);
            return Results.Ok(result);
        });

        // 3. Ticket Management
        group.MapGet("/tickets", async ([FromQuery] string? status, ISupportTicketService service) =>
        {
            var tickets = await service.GetTicketsAsync(status);
            return Results.Ok(tickets);
        });

        group.MapPost("/tickets", async ([FromBody] CreateTicketRequest req, ISupportTicketService service) =>
        {
            if (string.IsNullOrWhiteSpace(req.Subject) || string.IsNullOrWhiteSpace(req.DomainName))
                return Results.BadRequest(new { message = "Subject and Domain are required." });

            var ticket = await service.CreateTicketAsync(req);
            return Results.Created($"/api/support/tickets/{ticket.Id}", ticket);
        });

        group.MapPost("/tickets/{id:guid}/resolve", async (Guid id, [FromBody] ResolveTicketRequest req, ISupportTicketService service) =>
        {
            var success = await service.ResolveTicketAsync(id, req);
            return success ? Results.Ok(new { message = "Ticket marked as Resolved." }) : Results.NotFound();
        });

        // 4. Mailbox Quota Diagnostics & Adjustment
        group.MapGet("/mailbox/quota", async ([FromQuery] string address, ISupportTicketService service) =>
        {
            if (string.IsNullOrWhiteSpace(address))
                return Results.BadRequest(new { message = "Mailbox address is required." });

            var quota = await service.GetMailboxQuotaAsync(address);
            return quota != null ? Results.Ok(quota) : Results.NotFound();
        });

        group.MapPost("/mailbox/quota/adjust", async ([FromBody] AdjustQuotaPayload payload, ISupportTicketService service) =>
        {
            if (string.IsNullOrWhiteSpace(payload.Address))
                return Results.BadRequest(new { message = "Mailbox address is required." });

            var success = await service.AdjustMailboxQuotaAsync(
                payload.Address,
                new AdjustQuotaRequest(payload.AdditionalBytes, payload.Reason ?? "Temporary customer quota expansion")
            );

            return success ? Results.Ok(new { message = "Quota adjustment granted and logged." }) : Results.NotFound();
        });

        // 5. Staff Audit Logs
        group.MapGet("/audit-logs", async (FlitflapMasterDbContext masterDb, [FromQuery] int limit = 50) =>
        {
            var logs = await masterDb.StaffAuditLogs
                .OrderByDescending(l => l.TimestampUtc)
                .Take(limit)
                .ToListAsync();

            return Results.Ok(logs);
        });
    }
}

