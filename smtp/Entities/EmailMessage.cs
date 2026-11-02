namespace EmailHost.Smtp.Entities;

public class EmailMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MailboxId { get; set; }
    public Mailbox? Mailbox { get; set; }

    public string MessageId { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Folder { get; set; } = "Inbox"; // Inbox, Sent, Spam, Trash
    public bool IsRead { get; set; }
    public double SpamScore { get; set; }
    public bool VirusDetected { get; set; }
    public string? VirusName { get; set; }
    public string RelativeFilePath { get; set; } = string.Empty;
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
}

