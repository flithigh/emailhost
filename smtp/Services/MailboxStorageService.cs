using EmailHost.Smtp.Data;
using EmailHost.Smtp.Entities;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace EmailHost.Smtp.Services;

public class MailboxStorageService
{
    private readonly string _storageRoot;
    private readonly QuotaManager _quotaManager;

    public MailboxStorageService(string storageRoot = "storage/mailboxes")
    {
        _storageRoot = storageRoot;
        _quotaManager = new QuotaManager();
        if (!Directory.Exists(_storageRoot))
        {
            Directory.CreateDirectory(_storageRoot);
        }
    }

    public async Task<(bool Success, string ErrorMessage, EmailMessage? SavedMessage)> StoreMessageAsync(
        Mailbox mailbox,
        MimeMessage mimeMessage,
        byte[] rawBytes,
        string folder,
        double spamScore,
        bool virusDetected,
        string? virusName,
        SmtpDbContext db)
    {
        // 1. Quota Check
        var quotaCheck = _quotaManager.CheckQuota(mailbox, rawBytes.Length);
        if (!quotaCheck.Allowed)
        {
            return (false, quotaCheck.ErrorMessage, null);
        }

        // 2. Prepare File System Path: storage/mailboxes/{domain}/{user}/{folder}/
        var domainName = mailbox.Domain?.Name ?? "unknown_domain";
        var userPart = mailbox.LocalPart;
        var folderDir = Path.Combine(_storageRoot, domainName, userPart, folder);
        Directory.CreateDirectory(folderDir);

        var fileMessageId = Guid.NewGuid().ToString("N");
        var fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{fileMessageId}.eml";
        var absolutePath = Path.Combine(folderDir, fileName);
        var relativePath = Path.Combine(domainName, userPart, folder, fileName);

        // 3. Write .eml to File System
        await File.WriteAllBytesAsync(absolutePath, rawBytes);

        // 4. Record metadata in PostgreSQL database
        var emailEntity = new EmailMessage
        {
            MailboxId = mailbox.Id,
            MessageId = mimeMessage.MessageId ?? $"<{fileMessageId}@{domainName}>",
            From = mimeMessage.From.ToString(),
            To = mimeMessage.To.ToString(),
            Subject = mimeMessage.Subject ?? "(No Subject)",
            SizeBytes = rawBytes.Length,
            Folder = folder,
            IsRead = false,
            SpamScore = spamScore,
            VirusDetected = virusDetected,
            VirusName = virusName,
            RelativeFilePath = relativePath,
            ReceivedAtUtc = DateTime.UtcNow
        };

        db.Messages.Add(emailEntity);

        // 5. Update Mailbox storage usage
        mailbox.UsedBytes += rawBytes.Length;
        db.Mailboxes.Update(mailbox);

        await db.SaveChangesAsync();

        return (true, string.Empty, emailEntity);
    }

    public async Task<bool> DeleteMessageAsync(Guid messageId, SmtpDbContext db)
    {
        var message = await db.Messages.Include(m => m.Mailbox).FirstOrDefaultAsync(m => m.Id == messageId);
        if (message == null) return false;

        // Delete file from disk
        var fullPath = Path.Combine(_storageRoot, message.RelativeFilePath);
        if (File.Exists(fullPath))
        {
            try { File.Delete(fullPath); } catch { /* log or ignore */ }
        }

        // Decrement quota
        if (message.Mailbox != null)
        {
            message.Mailbox.UsedBytes = Math.Max(0, message.Mailbox.UsedBytes - message.SizeBytes);
            db.Mailboxes.Update(message.Mailbox);
        }

        db.Messages.Remove(message);
        await db.SaveChangesAsync();
        return true;
    }
}

