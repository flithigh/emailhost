using EmailHost.Support.Data;
using EmailHost.Support.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmailHost.Support.Services;

public record CreateTicketRequest(
    string DomainName,
    string ContactEmail,
    string Subject,
    string Category,
    string Priority,
    string Description
);

public record ResolveTicketRequest(string ResolutionNotes);
public record AdjustQuotaRequest(long AdditionalBytes, string Reason);

public record MailboxQuotaDto(
    Guid Id,
    string Address,
    string DomainName,
    long QuotaBytes,
    long UsedBytes,
    double PercentUsed,
    bool IsActive
);

public interface ISupportTicketService
{
    Task<List<SupportTicket>> GetTicketsAsync(string? status = null);
    Task<SupportTicket> CreateTicketAsync(CreateTicketRequest req, string staffUser = "support@flitflap.com");
    Task<bool> ResolveTicketAsync(Guid id, ResolveTicketRequest req, string staffUser = "support@flitflap.com");
    Task<MailboxQuotaDto?> GetMailboxQuotaAsync(string address);
    Task<bool> AdjustMailboxQuotaAsync(string address, AdjustQuotaRequest req, string staffUser = "support@flitflap.com");
}

public class SupportTicketService : ISupportTicketService
{
    private readonly FlitflapTenantDbContext _tenantDb;
    private readonly FlitflapMasterDbContext _masterDb;

    public SupportTicketService(FlitflapTenantDbContext tenantDb, FlitflapMasterDbContext masterDb)
    {
        _tenantDb = tenantDb;
        _masterDb = masterDb;
    }

    public async Task<List<SupportTicket>> GetTicketsAsync(string? status = null)
    {
        var q = _tenantDb.SupportTickets.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            q = q.Where(t => t.Status.ToLower() == status.ToLower());
        }

        return await q.OrderByDescending(t => t.CreatedAtUtc).ToListAsync();
    }

    public async Task<SupportTicket> CreateTicketAsync(CreateTicketRequest req, string staffUser = "support@flitflap.com")
    {
        var ticket = new SupportTicket
        {
            DomainName = req.DomainName.Trim(),
            ContactEmail = req.ContactEmail.Trim(),
            Subject = req.Subject.Trim(),
            Category = req.Category,
            Priority = req.Priority,
            Status = "Open",
            Description = req.Description,
            StaffAssignee = staffUser,
            CreatedAtUtc = DateTime.UtcNow
        };

        _tenantDb.SupportTickets.Add(ticket);

        _masterDb.StaffAuditLogs.Add(new StaffAuditLog
        {
            StaffUser = staffUser,
            Action = "SupportTicketCreated",
            TargetTenantOrDomain = ticket.DomainName,
            Details = $"Ticket {ticket.TicketNumber} created for {ticket.DomainName} ({ticket.Category})"
        });

        await _tenantDb.SaveChangesAsync();
        await _masterDb.SaveChangesAsync();

        return ticket;
    }

    public async Task<bool> ResolveTicketAsync(Guid id, ResolveTicketRequest req, string staffUser = "support@flitflap.com")
    {
        var ticket = await _tenantDb.SupportTickets.FindAsync(id);
        if (ticket == null) return false;

        ticket.Status = "Resolved";
        ticket.ResolutionNotes = req.ResolutionNotes;
        ticket.ResolvedAtUtc = DateTime.UtcNow;

        _masterDb.StaffAuditLogs.Add(new StaffAuditLog
        {
            StaffUser = staffUser,
            Action = "SupportTicketResolved",
            TargetTenantOrDomain = ticket.DomainName,
            Details = $"Ticket {ticket.TicketNumber} resolved: {req.ResolutionNotes}"
        });

        await _tenantDb.SaveChangesAsync();
        await _masterDb.SaveChangesAsync();

        return true;
    }

    public async Task<MailboxQuotaDto?> GetMailboxQuotaAsync(string address)
    {
        var clean = address.Trim().ToLowerInvariant();
        var mb = await _tenantDb.Mailboxes.FirstOrDefaultAsync(m => m.Address.ToLower() == clean);
        if (mb == null) return null;

        double pct = mb.QuotaBytes > 0 ? ((double)mb.UsedBytes / mb.QuotaBytes) * 100.0 : 0.0;

        return new MailboxQuotaDto(
            Id: mb.Id,
            Address: mb.Address,
            DomainName: mb.DomainName,
            QuotaBytes: mb.QuotaBytes,
            UsedBytes: mb.UsedBytes,
            PercentUsed: Math.Round(pct, 1),
            IsActive: mb.IsActive
        );
    }

    public async Task<bool> AdjustMailboxQuotaAsync(string address, AdjustQuotaRequest req, string staffUser = "support@flitflap.com")
    {
        var clean = address.Trim().ToLowerInvariant();
        var mb = await _tenantDb.Mailboxes.FirstOrDefaultAsync(m => m.Address.ToLower() == clean);
        if (mb == null) return false;

        var prevGb = mb.QuotaBytes / (1024L * 1024L * 1024L);
        mb.QuotaBytes += req.AdditionalBytes;
        var newGb = mb.QuotaBytes / (1024L * 1024L * 1024L);

        _masterDb.StaffAuditLogs.Add(new StaffAuditLog
        {
            StaffUser = staffUser,
            Action = "MailboxQuotaAdjusted",
            TargetTenantOrDomain = mb.Address,
            Details = $"Support granted temporary quota headroom to {mb.Address}: {prevGb} GB -> {newGb} GB. Reason: {req.Reason}"
        });

        await _tenantDb.SaveChangesAsync();
        await _masterDb.SaveChangesAsync();

        return true;
    }
}

