using DnsClient;
using EmailHost.Support.Data;
using EmailHost.Support.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmailHost.Support.Services;

public record DnsRecordCheckDto(string RecordType, string Hostname, bool IsValid, string FoundValue, string ExpectedValue);

public record TenantDnsDiagnosticDto(
    string DomainName,
    bool OverallHealthy,
    List<DnsRecordCheckDto> Records,
    string CloudflareInstructions,
    string GoDaddyInstructions,
    DateTime DiagnosedAtUtc
);

public interface ITenantDiagnosticService
{
    Task<List<Tenant>> SearchTenantsAsync(string query);
    Task<TenantDnsDiagnosticDto> DiagnoseDomainDnsAsync(string domainName, string staffUser = "support@flitflap.com");
}

public class TenantDiagnosticService : ITenantDiagnosticService
{
    private readonly FlitflapMasterDbContext _masterDb;
    private readonly ILookupClient _dnsClient;

    public TenantDiagnosticService(FlitflapMasterDbContext masterDb)
    {
        _masterDb = masterDb;
        _dnsClient = new LookupClient(NameServer.GooglePublicDns, NameServer.Cloudflare);
    }

    public async Task<List<Tenant>> SearchTenantsAsync(string query)
    {
        var q = _masterDb.Tenants.Include(t => t.Domains).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var s = query.Trim().ToLowerInvariant();
            q = q.Where(t => t.Name.ToLower().Contains(s) ||
                             t.ContactEmail.ToLower().Contains(s) ||
                             t.Domains.Any(d => d.DomainName.ToLower().Contains(s)));
        }

        return await q.OrderBy(t => t.Name).Take(20).ToListAsync();
    }

    public async Task<TenantDnsDiagnosticDto> DiagnoseDomainDnsAsync(string domainName, string staffUser = "support@flitflap.com")
    {
        var domain = domainName.Trim().ToLowerInvariant();
        var records = new List<DnsRecordCheckDto>();

        // 1. Check MX Record
        bool mxValid = false;
        string foundMx = "None";
        try
        {
            var mxQuery = await _dnsClient.QueryAsync(domain, QueryType.MX);
            var mxList = mxQuery.Answers.MxRecords().ToList();
            if (mxList.Any())
            {
                foundMx = string.Join(", ", mxList.Select(m => $"{m.Exchange.Value.TrimEnd('.')} (prio {m.Preference})"));
                mxValid = true;
            }
        }
        catch { }

        records.Add(new DnsRecordCheckDto(
            RecordType: "MX",
            Hostname: "@",
            IsValid: mxValid,
            FoundValue: foundMx,
            ExpectedValue: "mail.flitflap.com (Priority 10)"
        ));

        // 2. Check SPF Record (TXT @)
        bool spfValid = false;
        string foundSpf = "None";
        try
        {
            var txtQuery = await _dnsClient.QueryAsync(domain, QueryType.TXT);
            foreach (var txt in txtQuery.Answers.TxtRecords().SelectMany(r => r.Text))
            {
                if (txt.StartsWith("v=spf1", StringComparison.OrdinalIgnoreCase))
                {
                    foundSpf = txt;
                    spfValid = true;
                    break;
                }
            }
        }
        catch { }

        records.Add(new DnsRecordCheckDto(
            RecordType: "TXT (SPF)",
            Hostname: "@",
            IsValid: spfValid,
            FoundValue: foundSpf,
            ExpectedValue: "v=spf1 mx include:spf.flitflap.com ~all"
        ));

        // 3. Check DKIM Record (TXT default._domainkey.{domain})
        bool dkimValid = false;
        string foundDkim = "None";
        try
        {
            var dkimQuery = await _dnsClient.QueryAsync($"default._domainkey.{domain}", QueryType.TXT);
            foreach (var txt in dkimQuery.Answers.TxtRecords().SelectMany(r => r.Text))
            {
                if (txt.StartsWith("v=DKIM1", StringComparison.OrdinalIgnoreCase))
                {
                    foundDkim = txt.Length > 40 ? txt.Substring(0, 40) + "..." : txt;
                    dkimValid = true;
                    break;
                }
            }
        }
        catch { }

        records.Add(new DnsRecordCheckDto(
            RecordType: "TXT (DKIM)",
            Hostname: $"default._domainkey.{domain}",
            IsValid: dkimValid,
            FoundValue: foundDkim,
            ExpectedValue: "v=DKIM1; k=rsa; p=MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8A..."
        ));

        // 4. Check DMARC Record (TXT _dmarc.{domain})
        bool dmarcValid = false;
        string foundDmarc = "None";
        try
        {
            var dmarcQuery = await _dnsClient.QueryAsync($"_dmarc.{domain}", QueryType.TXT);
            foreach (var txt in dmarcQuery.Answers.TxtRecords().SelectMany(r => r.Text))
            {
                if (txt.StartsWith("v=DMARC1", StringComparison.OrdinalIgnoreCase))
                {
                    foundDmarc = txt;
                    dmarcValid = true;
                    break;
                }
            }
        }
        catch { }

        records.Add(new DnsRecordCheckDto(
            RecordType: "TXT (DMARC)",
            Hostname: $"_dmarc.{domain}",
            IsValid: dmarcValid,
            FoundValue: foundDmarc,
            ExpectedValue: $"v=DMARC1; p=quarantine; rua=mailto:dmarc@{domain}"
        ));

        bool overall = mxValid && (spfValid || dkimValid);

        // Assisted Instructions generator
        var cfInstructions = $@"# Cloudflare DNS Configuration for {domain}:
Type: MX    | Name: @                   | Target: mail.flitflap.com | Priority: 10
Type: TXT   | Name: @                   | Content: v=spf1 mx include:spf.flitflap.com ~all
Type: TXT   | Name: default._domainkey  | Content: v=DKIM1; k=rsa; p=MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8A...
Type: TXT   | Name: _dmarc              | Content: v=DMARC1; p=quarantine; rua=mailto:dmarc@{domain}
Type: CNAME | Name: webmail             | Target: webmail.flitflap.com";

        var gdInstructions = $@"# GoDaddy / Namecheap DNS Configuration for {domain}:
Host: @                   | Type: MX    | Points to: mail.flitflap.com | Priority: 10
Host: @                   | Type: TXT   | Value: v=spf1 mx include:spf.flitflap.com ~all
Host: default._domainkey  | Type: TXT   | Value: v=DKIM1; k=rsa; p=...
Host: _dmarc              | Type: TXT   | Value: v=DMARC1; p=quarantine; rua=mailto:dmarc@{domain}";

        // Audit Trail
        _masterDb.StaffAuditLogs.Add(new StaffAuditLog
        {
            StaffUser = staffUser,
            Action = "DnsDiagnosed",
            TargetTenantOrDomain = domain,
            Details = $"Support engineer performed live public DNS diagnostic for domain '{domain}'. Overall Status: {(overall ? "Healthy" : "Action Required")}"
        });
        await _masterDb.SaveChangesAsync();

        return new TenantDnsDiagnosticDto(
            DomainName: domain,
            OverallHealthy: overall,
            Records: records,
            CloudflareInstructions: cfInstructions,
            GoDaddyInstructions: gdInstructions,
            DiagnosedAtUtc: DateTime.UtcNow
        );
    }
}

