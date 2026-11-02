namespace EmailHost.Smtp.Entities;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string ClientIp { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

