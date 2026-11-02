using EmailHost.Smtp.Entities;

namespace EmailHost.Smtp.Services;

public class QuotaManager
{
    public (bool Allowed, string ErrorMessage) CheckQuota(Mailbox mailbox, long incomingMessageSizeBytes)
    {
        if (!mailbox.IsActive)
        {
            return (false, "Mailbox is disabled.");
        }

        if (mailbox.QuotaBytes > 0 && (mailbox.UsedBytes + incomingMessageSizeBytes) > mailbox.QuotaBytes)
        {
            var usedMb = mailbox.UsedBytes / (1024 * 1024);
            var limitMb = mailbox.QuotaBytes / (1024 * 1024);
            return (false, $"Mailbox quota exceeded. Used: {usedMb} MB / Limit: {limitMb} MB.");
        }

        return (true, string.Empty);
    }
}

