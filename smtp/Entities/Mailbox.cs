namespace EmailHost.Smtp.Entities;

public class Mailbox
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DomainId { get; set; }
    public Domain? Domain { get; set; }
    public string LocalPart { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public long QuotaBytes { get; set; } = 1024L * 1024L * 1024L; // Default 1 GB
    public long UsedBytes { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastPasswordResetAtUtc { get; set; }

    public List<EmailMessage> Messages { get; set; } = new();
}

