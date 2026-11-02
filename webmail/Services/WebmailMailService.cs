using EmailHost.Webmail.Data;
using EmailHost.Webmail.Entities;
using EmailHost.Webmail.Storage;
using MailKit.Net.Smtp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace EmailHost.Webmail.Services;

public record FolderStatsDto(string Folder, int TotalCount, int UnreadCount);
public record AttachmentDto(string FileName, string ContentType, long SizeBytes);

public record MessageSummaryDto(
    Guid Id,
    string Folder,
    string Sender,
    string Recipient,
    string Subject,
    string PreviewSnippet,
    long SizeBytes,
    bool IsRead,
    bool IsFlagged,
    double SpamScore,
    string SpfVerdict,
    string DkimVerdict,
    string DmarcVerdict,
    DateTime ReceivedAtUtc,
    bool HasAttachments
);

public record MessageDetailDto(
    Guid Id,
    string Folder,
    string Sender,
    string Recipient,
    string Cc,
    string Subject,
    string? HtmlBody,
    string? TextBody,
    long SizeBytes,
    bool IsRead,
    bool IsFlagged,
    double SpamScore,
    string SpfVerdict,
    string DkimVerdict,
    string DmarcVerdict,
    DateTime ReceivedAtUtc,
    List<AttachmentDto> Attachments
);

public class SendEmailRequest
{
    public string To { get; set; } = string.Empty;
    public string Cc { get; set; } = string.Empty;
    public string Bcc { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public string? ReplyToMessageId { get; set; }
    public List<EmailAttachmentUpload>? Attachments { get; set; }
}

public class EmailAttachmentUpload
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Data { get; set; } = Array.Empty<byte>();
}

public interface IWebmailMailService
{
    Task<List<FolderStatsDto>> GetFolderStatsAsync(Guid mailboxId);
    Task<(List<MessageSummaryDto> Items, int TotalCount)> GetMessagesPagedAsync(Guid mailboxId, string folder, string? search, int page, int pageSize);
    Task<MessageDetailDto?> GetMessageDetailAsync(Guid mailboxId, Guid messageId);
    Task<(Stream? Stream, string FileName)> GetRawEmlStreamAsync(Guid mailboxId, Guid messageId);
    Task<(Stream? Stream, string ContentType, string FileName)?> GetAttachmentStreamAsync(Guid mailboxId, Guid messageId, string fileName);
    Task<Guid> SendEmailAsync(Guid mailboxId, SendEmailRequest request);
    Task<Guid> SaveDraftAsync(Guid mailboxId, SendEmailRequest request);
    Task<bool> ToggleFlagAsync(Guid mailboxId, Guid messageId, bool? isFlagged = null);
    Task<bool> ToggleReadAsync(Guid mailboxId, Guid messageId, bool? isRead = null);
    Task<bool> MoveMessageAsync(Guid mailboxId, Guid messageId, string targetFolder);
    Task<bool> DeleteMessageAsync(Guid mailboxId, Guid messageId);
}

public class WebmailMailService : IWebmailMailService
{
    private readonly FlitflapTenantDbContext _tenantDb;
    private readonly FlitflapMasterDbContext _masterDb;
    private readonly IGarageStorageService _garage;
    private readonly IConfiguration _config;
    private readonly ILogger<WebmailMailService> _logger;

    public WebmailMailService(
        FlitflapTenantDbContext tenantDb,
        FlitflapMasterDbContext masterDb,
        IGarageStorageService garage,
        IConfiguration config,
        ILogger<WebmailMailService> logger)
    {
        _tenantDb = tenantDb;
        _masterDb = masterDb;
        _garage = garage;
        _config = config;
        _logger = logger;
    }

    public async Task<List<FolderStatsDto>> GetFolderStatsAsync(Guid mailboxId)
    {
        var standardFolders = new[] { "INBOX", "Sent", "Drafts", "Trash", "Spam" };
        var list = new List<FolderStatsDto>();

        var query = _tenantDb.MessagesIndex
            .Where(m => m.MailboxId == mailboxId && !m.IsDeleted);

        var grouped = await query
            .GroupBy(m => m.Folder)
            .Select(g => new
            {
                Folder = g.Key,
                TotalCount = g.Count(),
                UnreadCount = g.Count(m => !m.IsRead)
            })
            .ToListAsync();

        var starredCount = await query.CountAsync(m => m.IsFlagged);
        list.Add(new FolderStatsDto("Starred", starredCount, 0));

        foreach (var f in standardFolders)
        {
            var match = grouped.FirstOrDefault(g => g.Folder.Equals(f, StringComparison.OrdinalIgnoreCase));
            list.Add(new FolderStatsDto(f, match?.TotalCount ?? 0, match?.UnreadCount ?? 0));
        }

        return list;
    }

    public async Task<(List<MessageSummaryDto> Items, int TotalCount)> GetMessagesPagedAsync(Guid mailboxId, string folder, string? search, int page, int pageSize)
    {
        var query = _tenantDb.MessagesIndex
            .Where(m => m.MailboxId == mailboxId && !m.IsDeleted);

        if (folder.Equals("Starred", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(m => m.IsFlagged);
        }
        else
        {
            query = query.Where(m => m.Folder.ToLower() == folder.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(m =>
                m.Subject.ToLower().Contains(s) ||
                m.Sender.ToLower().Contains(s) ||
                m.Recipient.ToLower().Contains(s) ||
                m.PreviewSnippet.ToLower().Contains(s));
        }

        int totalCount = await query.CountAsync();

        var records = await query
            .OrderByDescending(m => m.ReceivedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MessageSummaryDto(
                m.Id,
                m.Folder,
                m.Sender,
                m.Recipient,
                string.IsNullOrEmpty(m.Subject) ? "(No Subject)" : m.Subject,
                m.PreviewSnippet,
                m.SizeBytes,
                m.IsRead,
                m.IsFlagged,
                m.SpamScore,
                m.SpfVerdict,
                m.DkimVerdict,
                m.DmarcVerdict,
                m.ReceivedAtUtc,
                m.SizeBytes > 2000 // Heuristic indicator for attachments
            ))
            .ToListAsync();

        return (records, totalCount);
    }

    public async Task<MessageDetailDto?> GetMessageDetailAsync(Guid mailboxId, Guid messageId)
    {
        var msg = await _tenantDb.MessagesIndex
            .FirstOrDefaultAsync(m => m.Id == messageId && m.MailboxId == mailboxId && !m.IsDeleted);

        if (msg == null) return null;

        // Auto mark as read
        if (!msg.IsRead)
        {
            msg.IsRead = true;
            await _tenantDb.SaveChangesAsync();
        }

        string? htmlBody = null;
        string? textBody = null;
        var attachments = new List<AttachmentDto>();

        if (!string.IsNullOrEmpty(msg.GarageStorageKey))
        {
            try
            {
                using var stream = await _garage.RetrieveStreamAsync(msg.GarageStorageKey);
                if (stream != null)
                {
                    var mime = await MimeMessage.LoadAsync(stream);
                    htmlBody = mime.HtmlBody;
                    textBody = mime.TextBody;

                    foreach (var part in mime.Attachments)
                    {
                        var fn = part.ContentType.Name ?? (part is MimePart mp ? mp.FileName : "attachment");
                        long size = 0;
                        if (part is MimePart mp2 && mp2.Content != null)
                        {
                            using var ms = new MemoryStream();
                            mp2.Content.DecodeTo(ms);
                            size = ms.Length;
                        }
                        attachments.Add(new AttachmentDto(fn ?? "attachment", part.ContentType.MimeType, size));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to parse MIME message {Key}: {Error}", msg.GarageStorageKey, ex.Message);
            }
        }

        if (string.IsNullOrEmpty(htmlBody) && string.IsNullOrEmpty(textBody))
        {
            textBody = string.IsNullOrEmpty(msg.PreviewSnippet) ? "(Message content empty or archived)" : msg.PreviewSnippet;
        }

        return new MessageDetailDto(
            msg.Id,
            msg.Folder,
            msg.Sender,
            msg.Recipient,
            msg.Cc,
            string.IsNullOrEmpty(msg.Subject) ? "(No Subject)" : msg.Subject,
            htmlBody,
            textBody,
            msg.SizeBytes,
            msg.IsRead,
            msg.IsFlagged,
            msg.SpamScore,
            msg.SpfVerdict,
            msg.DkimVerdict,
            msg.DmarcVerdict,
            msg.ReceivedAtUtc,
            attachments
        );
    }

    public async Task<(Stream? Stream, string FileName)> GetRawEmlStreamAsync(Guid mailboxId, Guid messageId)
    {
        var msg = await _tenantDb.MessagesIndex
            .FirstOrDefaultAsync(m => m.Id == messageId && m.MailboxId == mailboxId && !m.IsDeleted);

        if (msg == null || string.IsNullOrEmpty(msg.GarageStorageKey))
            return (null, "message.eml");

        var stream = await _garage.RetrieveStreamAsync(msg.GarageStorageKey);
        var filename = $"{msg.Subject.Replace(" ", "_").Replace("/", "_")}_{msg.Id}.eml";
        return (stream, filename);
    }

    public async Task<(Stream? Stream, string ContentType, string FileName)?> GetAttachmentStreamAsync(Guid mailboxId, Guid messageId, string fileName)
    {
        var msg = await _tenantDb.MessagesIndex
            .FirstOrDefaultAsync(m => m.Id == messageId && m.MailboxId == mailboxId && !m.IsDeleted);

        if (msg == null || string.IsNullOrEmpty(msg.GarageStorageKey)) return null;

        using var stream = await _garage.RetrieveStreamAsync(msg.GarageStorageKey);
        if (stream == null) return null;

        var mime = await MimeMessage.LoadAsync(stream);
        foreach (var part in mime.Attachments)
        {
            var fn = part.ContentType.Name ?? (part is MimePart mp ? mp.FileName : null);
            if (string.Equals(fn, fileName, StringComparison.OrdinalIgnoreCase) && part is MimePart mpTarget && mpTarget.Content != null)
            {
                var ms = new MemoryStream();
                mpTarget.Content.DecodeTo(ms);
                ms.Position = 0;
                return (ms, part.ContentType.MimeType, fn ?? fileName);
            }
        }

        return null;
    }

    public async Task<Guid> SendEmailAsync(Guid mailboxId, SendEmailRequest request)
    {
        var mailbox = await _tenantDb.Mailboxes.FindAsync(mailboxId);
        if (mailbox == null) throw new InvalidOperationException("Mailbox not found");

        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(string.IsNullOrWhiteSpace(mailbox.DisplayName) ? mailbox.Address : mailbox.DisplayName, mailbox.Address));

        foreach (var to in request.To.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            mimeMessage.To.Add(MailboxAddress.Parse(to.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(request.Cc))
        {
            foreach (var cc in request.Cc.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                mimeMessage.Cc.Add(MailboxAddress.Parse(cc.Trim()));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Bcc))
        {
            foreach (var bcc in request.Bcc.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                mimeMessage.Bcc.Add(MailboxAddress.Parse(bcc.Trim()));
            }
        }

        mimeMessage.Subject = request.Subject;

        var bodyBuilder = new BodyBuilder();
        var fullHtml = request.BodyHtml;
        if (!string.IsNullOrWhiteSpace(mailbox.SignatureHtml))
        {
            fullHtml += "<br><br>" + mailbox.SignatureHtml;
        }
        bodyBuilder.HtmlBody = fullHtml;

        if (request.Attachments != null)
        {
            foreach (var att in request.Attachments)
            {
                bodyBuilder.Attachments.Add(att.FileName, att.Data, ContentType.Parse(att.ContentType));
            }
        }

        mimeMessage.Body = bodyBuilder.ToMessageBody();

        // 1. Save to Garage Object Storage
        var messageGuid = Guid.NewGuid();
        var domain = await _masterDb.DomainsRegistry.FirstOrDefaultAsync(d => d.DomainName == mailbox.DomainName);
        var tenantUuid = domain != null ? domain.TenantId.ToString() : Guid.Empty.ToString();

        var garageKey = await _garage.SaveMimeMessageAsync(tenantUuid, mailbox.DomainName, mailbox.Id.ToString(), mimeMessage, messageGuid);

        using var ms = new MemoryStream();
        await mimeMessage.WriteToAsync(ms);
        long size = ms.Length;

        // 2. Index in Tenant DB under 'Sent'
        var index = new MessageIndex
        {
            Id = messageGuid,
            MailboxId = mailbox.Id,
            Folder = "Sent",
            Sender = mailbox.Address,
            Recipient = request.To,
            Cc = request.Cc ?? string.Empty,
            Subject = request.Subject,
            PreviewSnippet = request.BodyHtml.Length > 150 ? request.BodyHtml.Substring(0, 150) : request.BodyHtml,
            SizeBytes = size,
            IsRead = true,
            GarageStorageKey = garageKey,
            CreatedAtUtc = DateTime.UtcNow,
            ReceivedAtUtc = DateTime.UtcNow
        };

        _tenantDb.MessagesIndex.Add(index);
        mailbox.UsedBytes += size;
        await _tenantDb.SaveChangesAsync();

        // 3. Dispatch via SMTP relay (Port 587/25)
        _ = Task.Run(async () =>
        {
            try
            {
                var smtpHost = _config["SmtpRelay:Host"] ?? "localhost";
                var smtpPort = int.TryParse(_config["SmtpRelay:Port"], out var p) ? p : 587;
                using var client = new SmtpClient();
                await client.ConnectAsync(smtpHost, smtpPort, MailKit.Security.SecureSocketOptions.Auto);
                await client.SendAsync(mimeMessage);
                await client.DisconnectAsync(true);
                _logger.LogInformation("Webmail dispatched outbound message {Id} via SMTP {Host}:{Port}", messageGuid, smtpHost, smtpPort);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("SMTP relay delivery failed: {Error}. Message preserved in Sent folder and Garage storage.", ex.Message);
            }
        });

        return messageGuid;
    }

    public async Task<Guid> SaveDraftAsync(Guid mailboxId, SendEmailRequest request)
    {
        var mailbox = await _tenantDb.Mailboxes.FindAsync(mailboxId);
        if (mailbox == null) throw new InvalidOperationException("Mailbox not found");

        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(mailbox.DisplayName, mailbox.Address));
        if (!string.IsNullOrWhiteSpace(request.To))
        {
            foreach (var to in request.To.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                try { mimeMessage.To.Add(MailboxAddress.Parse(to.Trim())); } catch { }
            }
        }
        mimeMessage.Subject = request.Subject;

        var bodyBuilder = new BodyBuilder { HtmlBody = request.BodyHtml };
        mimeMessage.Body = bodyBuilder.ToMessageBody();

        var messageGuid = Guid.NewGuid();
        var domain = await _masterDb.DomainsRegistry.FirstOrDefaultAsync(d => d.DomainName == mailbox.DomainName);
        var tenantUuid = domain != null ? domain.TenantId.ToString() : Guid.Empty.ToString();

        var garageKey = await _garage.SaveMimeMessageAsync(tenantUuid, mailbox.DomainName, mailbox.Id.ToString(), mimeMessage, messageGuid);

        using var ms = new MemoryStream();
        await mimeMessage.WriteToAsync(ms);

        var index = new MessageIndex
        {
            Id = messageGuid,
            MailboxId = mailbox.Id,
            Folder = "Drafts",
            Sender = mailbox.Address,
            Recipient = request.To,
            Cc = request.Cc ?? string.Empty,
            Subject = string.IsNullOrEmpty(request.Subject) ? "(Draft)" : request.Subject,
            PreviewSnippet = request.BodyHtml.Length > 150 ? request.BodyHtml.Substring(0, 150) : request.BodyHtml,
            SizeBytes = ms.Length,
            IsRead = true,
            GarageStorageKey = garageKey,
            CreatedAtUtc = DateTime.UtcNow,
            ReceivedAtUtc = DateTime.UtcNow
        };

        _tenantDb.MessagesIndex.Add(index);
        await _tenantDb.SaveChangesAsync();
        return messageGuid;
    }

    public async Task<bool> ToggleFlagAsync(Guid mailboxId, Guid messageId, bool? isFlagged = null)
    {
        var msg = await _tenantDb.MessagesIndex.FirstOrDefaultAsync(m => m.Id == messageId && m.MailboxId == mailboxId);
        if (msg == null) return false;

        msg.IsFlagged = isFlagged ?? !msg.IsFlagged;
        await _tenantDb.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleReadAsync(Guid mailboxId, Guid messageId, bool? isRead = null)
    {
        var msg = await _tenantDb.MessagesIndex.FirstOrDefaultAsync(m => m.Id == messageId && m.MailboxId == mailboxId);
        if (msg == null) return false;

        msg.IsRead = isRead ?? !msg.IsRead;
        await _tenantDb.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MoveMessageAsync(Guid mailboxId, Guid messageId, string targetFolder)
    {
        var msg = await _tenantDb.MessagesIndex.FirstOrDefaultAsync(m => m.Id == messageId && m.MailboxId == mailboxId);
        if (msg == null) return false;

        msg.Folder = targetFolder;
        await _tenantDb.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteMessageAsync(Guid mailboxId, Guid messageId)
    {
        var msg = await _tenantDb.MessagesIndex.FirstOrDefaultAsync(m => m.Id == messageId && m.MailboxId == mailboxId);
        if (msg == null) return false;

        if (msg.Folder.Equals("Trash", StringComparison.OrdinalIgnoreCase))
        {
            // Permanent delete from Garage S3 and Database
            if (!string.IsNullOrEmpty(msg.GarageStorageKey))
            {
                await _garage.DeleteAsync(msg.GarageStorageKey);
            }
            _tenantDb.MessagesIndex.Remove(msg);
        }
        else
        {
            // Soft delete into Trash folder
            msg.Folder = "Trash";
        }

        await _tenantDb.SaveChangesAsync();
        return true;
    }
}
