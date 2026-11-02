using DnsClient;

namespace EmailHost.Smtp.Security;

public class DmarcResult
{
    public string Policy { get; set; } = "none"; // none, quarantine, reject
    public string Verdict { get; set; } = "None"; // Pass, Quarantine, Reject, None
    public bool SpfAligned { get; set; }
    public bool DkimAligned { get; set; }
    public string DmarcRecord { get; set; } = string.Empty;
}

public class DmarcEvaluator
{
    private readonly ILookupClient _dnsClient;

    public DmarcEvaluator()
    {
        _dnsClient = new LookupClient(NameServer.GooglePublicDns, NameServer.Cloudflare);
    }

    public async Task<DmarcResult> EvaluateAsync(string fromHeaderDomain, string spfVerdict, string spfDomain, DkimVerificationResult dkimResult)
    {
        var result = new DmarcResult();
        fromHeaderDomain = fromHeaderDomain.Trim().ToLowerInvariant();

        // 1. Check Alignment
        result.SpfAligned = (spfVerdict == "Pass") && fromHeaderDomain.Equals(spfDomain, StringComparison.OrdinalIgnoreCase);
        result.DkimAligned = (dkimResult.Status == "Pass") && fromHeaderDomain.Equals(dkimResult.Domain, StringComparison.OrdinalIgnoreCase);

        // 2. Query DMARC DNS Record: _dmarc.{domain}
        try
        {
            var dmarcHost = $"_dmarc.{fromHeaderDomain}";
            var query = await _dnsClient.QueryAsync(dmarcHost, QueryType.TXT);
            var record = query.Answers.TxtRecords()
                .SelectMany(r => r.Text)
                .FirstOrDefault(t => t.StartsWith("v=DMARC1", StringComparison.OrdinalIgnoreCase));

            if (record != null)
            {
                result.DmarcRecord = record;
                if (record.Contains("p=reject", StringComparison.OrdinalIgnoreCase))
                    result.Policy = "reject";
                else if (record.Contains("p=quarantine", StringComparison.OrdinalIgnoreCase))
                    result.Policy = "quarantine";
                else
                    result.Policy = "none";
            }
        }
        catch
        {
            result.Policy = "none";
        }

        // 3. Verdict calculation
        if (result.SpfAligned || result.DkimAligned)
        {
            result.Verdict = "Pass";
        }
        else
        {
            if (result.Policy == "reject")
                result.Verdict = "Reject";
            else if (result.Policy == "quarantine")
                result.Verdict = "Quarantine";
            else
                result.Verdict = "None";
        }

        return result;
    }
}

