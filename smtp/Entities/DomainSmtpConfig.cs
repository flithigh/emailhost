namespace EmailHost.Smtp.Entities;

public class DomainSmtpConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DomainId { get; set; }
    public Domain? Domain { get; set; }
    public string HostName { get; set; } = string.Empty;
    public int Port25 { get; set; } = 25;
    public int Port587 { get; set; } = 587;
    public int Port465 { get; set; } = 465;
    public bool RequireAuth { get; set; } = true;
    public bool SpooferDetection { get; set; } = true;
    public bool AntiSpam { get; set; } = true;
    public bool AntiVirus { get; set; } = true;
    public long MaxMessageSizeBytes { get; set; } = 25 * 1024 * 1024; // 25 MB
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

