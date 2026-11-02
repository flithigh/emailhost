namespace EmailHost.Support.Entities;

public class TenantMailbox
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Address { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string LocalPart { get; set; } = string.Empty;
    public string DomainName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public long QuotaBytes { get; set; } = 5L * 1024L * 1024L * 1024L; // 5 GB
    public long UsedBytes { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastPasswordResetAtUtc { get; set; }
}

public class MessageIndex
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MailboxId { get; set; }
    public string Folder { get; set; } = "INBOX";
    public string Sender { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string PreviewSnippet { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public bool IsRead { get; set; }
    public bool IsFlagged { get; set; }
    public bool IsDeleted { get; set; }
    public string GarageStorageKey { get; set; } = string.Empty;

    public double SpamScore { get; set; }
    public string SpfVerdict { get; set; } = "None";
    public string DkimVerdict { get; set; } = "None";
    public string DmarcVerdict { get; set; } = "None";
    public bool VirusDetected { get; set; }
    public string? VirusName { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
}

public class DeliveryTraceLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string MessageId { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string EventStatus { get; set; } = "Accepted"; // Accepted, Queued, Filtered, Deferred, Bounced, Delivered
    public string DiagnosticMessage { get; set; } = string.Empty;
    public string ClientIp { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

public class SupportTicket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TicketNumber { get; set; } = $"TKT-{Random.Shared.Next(1000, 9999)}";
    public string DomainName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Category { get; set; } = "DNS / Deliverability"; // DNS / Deliverability, Mailbox Quota, Authentication, Bounced Mail
    public string Priority { get; set; } = "Medium"; // Low, Medium, High, Urgent
    public string Status { get; set; } = "Open"; // Open, In Progress, Resolved
    public string Description { get; set; } = string.Empty;
    public string ResolutionNotes { get; set; } = string.Empty;
    public string StaffAssignee { get; set; } = "support@flitflap.com";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }
}

