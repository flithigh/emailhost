using System.Net.Sockets;
using System.Text;
using DnsClient;
using EmailHost.Smtp.Data;
using EmailHost.Smtp.Entities;
using EmailHost.Smtp.Security;
using EmailHost.Smtp.Storage;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace EmailHost.Smtp.Services;

public class DeliveryResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsLocalDelivery { get; set; }
    public string MessageId { get; set; } = string.Empty;
}

public class MessageRoutingService
{
    private readonly ILookupClient _dnsClient;
    private readonly IGarageStorageService _garageStorage;
    private readonly AntiSpamFilter _spamFilter;
    private readonly AntiVirusScanner _virusScanner;
    private readonly DkimService _dkimService;
    private readonly DmarcEvaluator _dmarcEvaluator;

    public MessageRoutingService(
        IGarageStorageService garageStorage,
        AntiSpamFilter spamFilter,
        AntiVirusScanner virusScanner,
        DkimService dkimService,
        DmarcEvaluator dmarcEvaluator)
    {
        _dnsClient = new LookupClient(NameServer.GooglePublicDns, NameServer.Cloudflare);
        _garageStorage = garageStorage;
        _spamFilter = spamFilter;
        _virusScanner = virusScanner;
        _dkimService = dkimService;
        _dmarcEvaluator = dmarcEvaluator;
    }

    public async Task<DeliveryResult> RouteAndDeliverAsync(
        string sender,
        string recipient,
        byte[] rawMessageBytes,
        string clientIp,
        string spfVerdict,
        string spfDomain,
        bool isAuthenticated,
        FlitflapMasterDbContext masterDb,
        FlitflapTenantDbContext tenantDb)
    {
        var recipientParts = recipient.Split('@');
        if (recipientParts.Length != 2)
        {
            return new DeliveryResult { Success = false, Message = "501 5.1.3 Bad recipient address syntax" };
        }

        var localPart = recipientParts[0].Trim().ToLowerInvariant();
        var domainPart = recipientParts[1].Trim().ToLowerInvariant();

        // 1. Parse MIME message
        using var stream = new MemoryStream(rawMessageBytes);
        var mimeMessage = await MimeMessage.LoadAsync(stream);
        var msgId = mimeMessage.MessageId ?? $"<{Guid.NewGuid():N}@{domainPart}>";

        // Record Initial Trace: Accepted
        await LogTraceAsync(tenantDb, msgId, sender, recipient, "Accepted", $"Connection from {clientIp} received message", clientIp);

        // 2. Anti-Virus Scanning
        var virusResult = await _virusScanner.ScanMessageAsync(mimeMessage, rawMessageBytes);
        if (virusResult.IsInfected)
        {
            await LogTraceAsync(tenantDb, msgId, sender, recipient, "Filtered", $"Virus blocked: {virusResult.ThreatName}", clientIp);
            return new DeliveryResult
            {
                Success = false,
                MessageId = msgId,
                Message = $"554 5.7.1 Message rejected: Security threat detected ({virusResult.ThreatName})"
            };
        }

        // 3. DKIM Verification
        var dkimResult = await _dkimService.VerifyDkimAsync(mimeMessage);

        // 4. DMARC Evaluation
        var fromHeaderDomain = mimeMessage.From.Mailboxes.FirstOrDefault()?.Domain ?? spfDomain;
        var dmarcResult = await _dmarcEvaluator.EvaluateAsync(fromHeaderDomain, spfVerdict, spfDomain, dkimResult);

        if (dmarcResult.Verdict == "Reject")
        {
            await LogTraceAsync(tenantDb, msgId, sender, recipient, "Filtered", $"DMARC policy 'reject' enforced for {fromHeaderDomain}", clientIp);
            return new DeliveryResult
            {
                Success = false,
                MessageId = msgId,
                Message = $"554 5.7.1 Message rejected due to DMARC policy for {fromHeaderDomain}"
            };
        }

        // 5. Anti-Spam Heuristics
        var spamResult = _spamFilter.Analyze(mimeMessage, spfVerdict);
        if (spamResult.ShouldReject)
        {
            await LogTraceAsync(tenantDb, msgId, sender, recipient, "Filtered", $"Spam score {spamResult.Score} exceeded rejection ceiling", clientIp);
            return new DeliveryResult
            {
                Success = false,
                MessageId = msgId,
                Message = $"554 5.7.1 Message rejected: spam score {spamResult.Score}"
            };
        }

        // 6. Check if recipient is a hosted domain in master registry
        var domainReg = await masterDb.DomainsRegistry.Include(d => d.Tenant)
            .FirstOrDefaultAsync(d => d.DomainName.ToLower() == domainPart && d.IsActive);

        if (domainReg != null)
        {
            // LOCAL INBOUND DELIVERY
            var mailbox = await tenantDb.Mailboxes.FirstOrDefaultAsync(m => m.Address.ToLower() == recipient.ToLower() || m.LocalPart.ToLower() == localPart);
            if (mailbox == null)
            {
                // Check aliases
                var alias = await tenantDb.Aliases.FirstOrDefaultAsync(a => a.SourceAlias.ToLower() == recipient.ToLower() && a.IsActive);
                if (alias != null)
                {
                    mailbox = await tenantDb.Mailboxes.FindAsync(alias.TargetMailboxId);
                }
            }

            if (mailbox == null || !mailbox.IsActive)
            {
                await LogTraceAsync(tenantDb, msgId, sender, recipient, "Bounced", $"User unknown: {recipient}", clientIp);
                return new DeliveryResult { Success = false, MessageId = msgId, Message = $"550 5.1.1 User unknown: <{recipient}>" };
            }

            // Quota Enforcement
            if (mailbox.QuotaBytes > 0 && (mailbox.UsedBytes + rawMessageBytes.Length) > mailbox.QuotaBytes)
            {
                await LogTraceAsync(tenantDb, msgId, sender, recipient, "Deferred", "Mailbox over quota", clientIp);
                return new DeliveryResult { Success = false, MessageId = msgId, Message = "452 4.2.2 Mailbox quota exceeded. Please try again later." };
            }

            var targetFolder = (spamResult.IsSpam || dmarcResult.Verdict == "Quarantine") ? "Spam" : "INBOX";
            var messageGuid = Guid.NewGuid();

            // Store in Garage S3 Object Storage
            var tenantId = domainReg.TenantId;
            var garageKey = await _garageStorage.StoreMessageAsync(tenantId, domainPart, mailbox.Id, messageGuid, rawMessageBytes);

            // Record Message in Tenant DB Index
            var msgIndex = new MessageIndex
            {
                Id = messageGuid,
                MailboxId = mailbox.Id,
                Folder = targetFolder,
                Sender = sender,
                Recipient = recipient,
                Subject = mimeMessage.Subject ?? "(No Subject)",
                PreviewSnippet = mimeMessage.TextBody?.Substring(0, Math.Min(mimeMessage.TextBody.Length, 150)) ?? string.Empty,
                SizeBytes = rawMessageBytes.Length,
                IsRead = false,
                GarageStorageKey = garageKey,
                SpamScore = spamResult.Score,
                SpfVerdict = spfVerdict,
                DkimVerdict = dkimResult.Status,
                DmarcVerdict = dmarcResult.Verdict,
                VirusDetected = virusResult.IsInfected,
                VirusName = virusResult.ThreatName,
                CreatedAtUtc = DateTime.UtcNow,
                ReceivedAtUtc = DateTime.UtcNow
            };

            tenantDb.MessagesIndex.Add(msgIndex);
            mailbox.UsedBytes += rawMessageBytes.Length;
            tenantDb.Mailboxes.Update(mailbox);
            await tenantDb.SaveChangesAsync();

            await LogTraceAsync(tenantDb, msgId, sender, recipient, "Delivered", $"Delivered to {targetFolder} via Garage ({garageKey})", clientIp);

            return new DeliveryResult
            {
                Success = true,
                IsLocalDelivery = true,
                MessageId = msgId,
                Message = "250 2.0.0 OK: Delivered to local mailbox"
            };
        }

        // 7. OUTBOUND REMOTE RELAY
        if (!isAuthenticated)
        {
            await LogTraceAsync(tenantDb, msgId, sender, recipient, "Filtered", "Relay denied for unauthenticated client", clientIp);
            return new DeliveryResult
            {
                Success = false,
                MessageId = msgId,
                Message = "550 5.7.1 Relaying denied: Authentication required for external destinations"
            };
        }

        // Outbound DKIM Signing
        var senderDomain = sender.Split('@').LastOrDefault() ?? "";
        var activeDkimKey = await tenantDb.DkimKeys.FirstOrDefaultAsync(k => k.DomainName.ToLower() == senderDomain.ToLower() && k.IsActive);
        byte[] payloadToSend = rawMessageBytes;

        if (activeDkimKey != null && !string.IsNullOrEmpty(activeDkimKey.PrivateKeyPem))
        {
            var signedMessage = await _dkimService.SignMessageAsync(mimeMessage, activeDkimKey.DomainName, activeDkimKey.Selector, activeDkimKey.PrivateKeyPem);
            using var outStream = new MemoryStream();
            await signedMessage.WriteToAsync(outStream);
            payloadToSend = outStream.ToArray();
        }

        await LogTraceAsync(tenantDb, msgId, sender, recipient, "Queued", "Queued for remote MX dispatch", clientIp);

        // Remote MX Relay
        var relayResult = await RelayToRemoteMxAsync(domainPart, sender, recipient, payloadToSend);
        await LogTraceAsync(tenantDb, msgId, sender, recipient, relayResult.Success ? "Delivered" : "Bounced", relayResult.Message, clientIp);

        return relayResult;
    }

    private async Task<DeliveryResult> RelayToRemoteMxAsync(string destinationDomain, string from, string to, byte[] rawBytes)
    {
        try
        {
            var mxQuery = await _dnsClient.QueryAsync(destinationDomain, QueryType.MX);
            var mxRecords = mxQuery.Answers.MxRecords().OrderBy(m => m.Preference).ToList();

            if (!mxRecords.Any())
            {
                return new DeliveryResult { Success = false, Message = $"550 5.1.2 Host unknown: No MX records found for '{destinationDomain}'" };
            }

            foreach (var mx in mxRecords)
            {
                var host = mx.Exchange.Value.TrimEnd('.');
                try
                {
                    using var tcpClient = new TcpClient();
                    var connectTask = tcpClient.ConnectAsync(host, 25);
                    if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask) continue;

                    using var stream = tcpClient.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII);
                    using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };

                    var banner = await reader.ReadLineAsync();
                    if (banner == null || !banner.StartsWith("220")) continue;

                    await writer.WriteLineAsync("EHLO mail.flitflap.com");
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null && line.Length >= 4 && line[3] == '-') { }

                    await writer.WriteLineAsync($"MAIL FROM:<{from}>");
                    var mailResp = await reader.ReadLineAsync();
                    if (mailResp == null || !mailResp.StartsWith("250")) continue;

                    await writer.WriteLineAsync($"RCPT TO:<{to}>");
                    var rcptResp = await reader.ReadLineAsync();
                    if (rcptResp == null || !rcptResp.StartsWith("250")) continue;

                    await writer.WriteLineAsync("DATA");
                    var dataResp = await reader.ReadLineAsync();
                    if (dataResp == null || !dataResp.StartsWith("354")) continue;

                    await stream.WriteAsync(rawBytes);
                    await writer.WriteLineAsync("\r\n.");
                    var finalResp = await reader.ReadLineAsync();
                    await writer.WriteLineAsync("QUIT");

                    return new DeliveryResult
                    {
                        Success = true,
                        IsLocalDelivery = false,
                        Message = $"250 2.0.0 Relayed successfully to {host}: {finalResp}"
                    };
                }
                catch { }
            }

            return new DeliveryResult { Success = false, Message = $"451 4.4.1 MX transmission failed for '{destinationDomain}'" };
        }
        catch (Exception ex)
        {
            return new DeliveryResult { Success = false, Message = $"451 4.4.0 DNS/Routing error: {ex.Message}" };
        }
    }

    private static async Task LogTraceAsync(
        FlitflapTenantDbContext tenantDb,
        string messageId,
        string sender,
        string recipient,
        string status,
        string message,
        string clientIp)
    {
        tenantDb.DeliveryTraceLogs.Add(new DeliveryTraceLog
        {
            MessageId = messageId,
            Sender = sender,
            Recipient = recipient,
            EventStatus = status,
            DiagnosticMessage = message,
            ClientIp = clientIp,
            TimestampUtc = DateTime.UtcNow
        });
        await tenantDb.SaveChangesAsync();
    }
}
