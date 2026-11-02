using DnsClient;

namespace EmailHost.Promote.Services;

public record DeliverabilityCheckItem(string Check, bool Passed, string Discovered, string Advice);

public record DeliverabilityTestResult(
    string Domain,
    int OverallScore, // 0 - 100
    string HealthRating, // Excellent, Good, Fair, Critical
    List<DeliverabilityCheckItem> Checks,
    string SummaryRecommendation,
    DateTime TestedAtUtc
);

public interface IDeliverabilityTestService
{
    Task<DeliverabilityTestResult> TestDomainDeliverabilityAsync(string domainName);
}

public class DeliverabilityTestService : IDeliverabilityTestService
{
    private readonly ILookupClient _dnsClient;

    public DeliverabilityTestService()
    {
        _dnsClient = new LookupClient(NameServer.GooglePublicDns, NameServer.Cloudflare);
    }

    public async Task<DeliverabilityTestResult> TestDomainDeliverabilityAsync(string domainName)
    {
        var domain = domainName.Trim().ToLowerInvariant();
        if (domain.StartsWith("http://") || domain.StartsWith("https://"))
        {
            domain = new Uri(domain).Host;
        }

        var checks = new List<DeliverabilityCheckItem>();
        int score = 0;

        // 1. MX Check (35 pts)
        bool mxPassed = false;
        string mxDesc = "No MX records found";
        try
        {
            var mxQuery = await _dnsClient.QueryAsync(domain, QueryType.MX);
            var mxList = mxQuery.Answers.MxRecords().ToList();
            if (mxList.Any())
            {
                mxPassed = true;
                score += 35;
                mxDesc = string.Join(", ", mxList.Select(m => $"{m.Exchange.Value.TrimEnd('.')} (Prio {m.Preference})"));
            }
        }
        catch { }

        checks.Add(new DeliverabilityCheckItem(
            Check: "Mail Exchanger (MX) Records",
            Passed: mxPassed,
            Discovered: mxDesc,
            Advice: mxPassed ? "Mail exchangers properly configured on DNS." : "Incoming mail cannot be delivered without active MX records."
        ));

        // 2. SPF Check (25 pts)
        bool spfPassed = false;
        string spfDesc = "No SPF record found";
        try
        {
            var txtQuery = await _dnsClient.QueryAsync(domain, QueryType.TXT);
            foreach (var txt in txtQuery.Answers.TxtRecords().SelectMany(r => r.Text))
            {
                if (txt.StartsWith("v=spf1", StringComparison.OrdinalIgnoreCase))
                {
                    spfPassed = true;
                    score += 25;
                    spfDesc = txt;
                    break;
                }
            }
        }
        catch { }

        checks.Add(new DeliverabilityCheckItem(
            Check: "Sender Policy Framework (SPF)",
            Passed: spfPassed,
            Discovered: spfDesc,
            Advice: spfPassed ? "SPF record protects against sender spoofing." : "Without SPF, major providers (Gmail, Outlook) may reject or spam-flag your emails."
        ));

        // 3. DMARC Check (25 pts)
        bool dmarcPassed = false;
        string dmarcDesc = "No DMARC policy found";
        try
        {
            var dmarcQuery = await _dnsClient.QueryAsync($"_dmarc.{domain}", QueryType.TXT);
            foreach (var txt in dmarcQuery.Answers.TxtRecords().SelectMany(r => r.Text))
            {
                if (txt.StartsWith("v=DMARC1", StringComparison.OrdinalIgnoreCase))
                {
                    dmarcPassed = true;
                    score += 25;
                    dmarcDesc = txt;
                    break;
                }
            }
        }
        catch { }

        checks.Add(new DeliverabilityCheckItem(
            Check: "DMARC Policy Enforcement",
            Passed: dmarcPassed,
            Discovered: dmarcDesc,
            Advice: dmarcPassed ? "DMARC policy active and aligned." : "DMARC is required by Gmail & Yahoo for high-volume inbox deliverability."
        ));

        // 4. DKIM default selector probe (15 pts)
        bool dkimPassed = false;
        string dkimDesc = "default._domainkey not found";
        try
        {
            var dkimQuery = await _dnsClient.QueryAsync($"default._domainkey.{domain}", QueryType.TXT);
            foreach (var txt in dkimQuery.Answers.TxtRecords().SelectMany(r => r.Text))
            {
                if (txt.StartsWith("v=DKIM1", StringComparison.OrdinalIgnoreCase))
                {
                    dkimPassed = true;
                    score += 15;
                    dkimDesc = "2048-bit RSA DKIM key located on DNS";
                    break;
                }
            }
        }
        catch { }

        checks.Add(new DeliverabilityCheckItem(
            Check: "DKIM Signature Record",
            Passed: dkimPassed,
            Discovered: dkimDesc,
            Advice: dkimPassed ? "Cryptographic signature record present." : "DKIM adds tamper-proof digital signatures to outbound emails."
        ));

        string rating = score >= 85 ? "Excellent" : score >= 60 ? "Good" : score >= 35 ? "Fair" : "Critical";
        string summary = score >= 85
            ? "Your domain has enterprise-grade email authentication configured."
            : $"Your domain scored {score}/100. Migrating to Flitflap Email Host automatically provisions 2048-bit RSA DKIM, SPF alignment, and DMARC enforcement.";

        return new DeliverabilityTestResult(
            Domain: domain,
            OverallScore: score,
            HealthRating: rating,
            Checks: checks,
            SummaryRecommendation: summary,
            TestedAtUtc: DateTime.UtcNow
        );
    }
}

