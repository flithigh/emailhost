namespace EmailHost.Smtp.Entities;

public class Domain
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public string VerificationToken { get; set; } = string.Empty;
    public string? MxRecord { get; set; }
    public bool SmtpEnabled { get; set; }
    public bool WebmailEnabled { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedAtUtc { get; set; }

    public List<Mailbox> Mailboxes { get; set; } = new();
    public DomainSmtpConfig? SmtpConfig { get; set; }
}

