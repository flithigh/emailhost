namespace EmailHost.ConsoleApp.Entities;

public class TenantMailbox
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Address { get; set; } = string.Empty;
    public string LocalPart { get; set; } = string.Empty;
    public string DomainName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public long QuotaBytes { get; set; } = 5L * 1024L * 1024L * 1024L; // Default 5 GB per doc.md
    public long UsedBytes { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastPasswordResetAtUtc { get; set; }

    public List<MessageIndex> Messages { get; set; } = new();
}

public class MailAlias
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SourceAlias { get; set; } = string.Empty;
    public Guid TargetMailboxId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class DkimKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DomainName { get; set; } = string.Empty;
    public string Selector { get; set; } = "default";
    public string PrivateKeyPem { get; set; } = string.Empty;
    public string PublicKeyPem { get; set; } = string.Empty;
    public string DnsTxtRecord { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class MessageIndex
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MailboxId { get; set; }
    public TenantMailbox? Mailbox { get; set; }

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

public class DomainFilter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FilterType { get; set; } = "Blacklist";
    public string Pattern { get; set; } = string.Empty;
    public string Action { get; set; } = "Reject";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class DeliveryTraceLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string MessageId { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string EventStatus { get; set; } = "Accepted";
    public string DiagnosticMessage { get; set; } = string.Empty;
    public string ClientIp { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

