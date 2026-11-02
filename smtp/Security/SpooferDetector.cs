using DnsClient;
using EmailHost.Smtp.Data;
using Microsoft.EntityFrameworkCore;

namespace EmailHost.Smtp.Security;

public class SpooferDetectionResult
{
    public bool IsSpoofed { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string SpfVerdict { get; set; } = "None"; // Pass, Fail, SoftFail, None
    public string SenderDomain { get; set; } = string.Empty;
}

public class SpooferDetector
{
    private readonly ILookupClient _dnsClient;

    public SpooferDetector()
    {
        _dnsClient = new LookupClient(NameServer.GooglePublicDns, NameServer.Cloudflare);
    }

    public async Task<SpooferDetectionResult> CheckSenderAsync(
        string senderEmail,
        string clientIp,
        bool isAuthenticated,
        FlitflapMasterDbContext masterDb)
    {
        var result = new SpooferDetectionResult();

        if (string.IsNullOrWhiteSpace(senderEmail))
        {
            result.IsSpoofed = true;
            result.Reason = "Empty or null sender address";
            return result;
        }

        var parts = senderEmail.Split('@');
        if (parts.Length != 2)
        {
            result.IsSpoofed = true;
            result.Reason = "Invalid email format";
            return result;
        }

        var domainPart = parts[1].Trim().ToLowerInvariant();
        result.SenderDomain = domainPart;

        // 1. Check if sender claims to be from a hosted domain in master registry
        var isHostedDomain = await masterDb.DomainsRegistry.AnyAsync(d => d.DomainName.ToLower() == domainPart && d.IsActive);
        if (isHostedDomain)
        {
            // If the sender domain is hosted on Flitflap, the client MUST be authenticated!
            if (!isAuthenticated)
            {
                result.IsSpoofed = true;
                result.Reason = $"Spoofing detected: Client claimed to be local hosted domain '{domainPart}' without valid authentication.";
                result.SpfVerdict = "Fail";
                return result;
            }

            // Authenticated local user sending from their own domain
            result.IsSpoofed = false;
            result.SpfVerdict = "Pass";
            return result;
        }

        // 2. For external domains, perform SPF check via DNS TXT records
        try
        {
            var txtRecords = await _dnsClient.QueryAsync(domainPart, QueryType.TXT);
            var spfRecord = txtRecords.Answers.TxtRecords()
                .SelectMany(r => r.Text)
                .FirstOrDefault(t => t.StartsWith("v=spf1", StringComparison.OrdinalIgnoreCase));

            if (spfRecord != null)
            {
                if (spfRecord.Contains("-all") && !spfRecord.Contains(clientIp))
                {
                    result.SpfVerdict = "Fail";
                    result.Reason = $"SPF -all policy rejected client IP {clientIp}";
                }
                else if (spfRecord.Contains("~all"))
                {
                    result.SpfVerdict = "SoftFail";
                }
                else
                {
                    result.SpfVerdict = "Pass";
                }
            }
        }
        catch
        {
            result.SpfVerdict = "Neutral";
        }

        return result;
    }
}
