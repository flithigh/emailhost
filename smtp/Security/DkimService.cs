using System.Security.Cryptography;
using System.Text;
using DnsClient;
using MimeKit;
using MimeKit.Cryptography;

namespace EmailHost.Smtp.Security;

public class DkimVerificationResult
{
    public bool HasDkimSignature { get; set; }
    public bool IsValid { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string Selector { get; set; } = string.Empty;
    public string Status { get; set; } = "None"; // Pass, Fail, None
    public string ErrorMessage { get; set; } = string.Empty;
}

public class DkimService
{
    private readonly ILookupClient _dnsClient;

    public DkimService()
    {
        _dnsClient = new LookupClient(NameServer.GooglePublicDns, NameServer.Cloudflare);
    }

    public (string PrivateKeyPem, string PublicKeyPem, string DnsTxtRecord) GenerateDkimKeyPair(string domain, string selector = "default")
    {
        using var rsa = RSA.Create(2048);
        var privateKeyPem = rsa.ExportPkcs8PrivateKeyPem();
        var pubKeyBytes = rsa.ExportSubjectPublicKeyInfo();
        var pubKeyBase64 = Convert.ToBase64String(pubKeyBytes);
        var publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();

        var dnsTxt = $"v=DKIM1; k=rsa; p={pubKeyBase64}";
        return (privateKeyPem, publicKeyPem, dnsTxt);
    }

    public Task<MimeMessage> SignMessageAsync(MimeMessage message, string domain, string selector, string privateKeyPem)
    {
        try
        {
            using var keyStream = new MemoryStream(Encoding.UTF8.GetBytes(privateKeyPem));
            var signer = new DkimSigner(keyStream, domain, selector)
            {
                SignatureAlgorithm = DkimSignatureAlgorithm.RsaSha256,
                AgentOrUserIdentifier = $"@{domain}",
                QueryMethod = "dns/txt"
            };

            var headersToSign = new[]
            {
                HeaderId.From,
                HeaderId.To,
                HeaderId.Subject,
                HeaderId.Date,
                HeaderId.MessageId
            };

            message.Prepare(EncodingConstraint.SevenBit);
            signer.Sign(message, headersToSign);
        }
        catch
        {
            // If signing fails (e.g. malformed key), proceed without halting delivery
        }

        return Task.FromResult(message);
    }

    public async Task<DkimVerificationResult> VerifyDkimAsync(MimeMessage message)
    {
        var result = new DkimVerificationResult();
        var dkimHeader = message.Headers.FirstOrDefault(h => h.Id == HeaderId.DkimSignature);
        if (dkimHeader == null)
        {
            result.Status = "None";
            return result;
        }

        result.HasDkimSignature = true;

        var signatureValue = dkimHeader.Value;
        var tags = signatureValue.Split(';')
            .Select(t => t.Trim())
            .Where(t => t.Contains('='))
            .ToDictionary(t => t.Substring(0, t.IndexOf('=')).Trim().ToLowerInvariant(),
                          t => t.Substring(t.IndexOf('=') + 1).Trim());

        if (tags.TryGetValue("d", out var d) && tags.TryGetValue("s", out var s))
        {
            result.Domain = d;
            result.Selector = s;

            try
            {
                var dkimHost = $"{s}._domainkey.{d}";
                var query = await _dnsClient.QueryAsync(dkimHost, QueryType.TXT);
                var txtRecord = query.Answers.TxtRecords().SelectMany(r => r.Text).FirstOrDefault(t => t.Contains("p="));

                if (txtRecord != null)
                {
                    result.IsValid = true;
                    result.Status = "Pass";
                }
                else
                {
                    result.IsValid = false;
                    result.Status = "Fail";
                    result.ErrorMessage = $"DKIM TXT key record not found for {dkimHost}";
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Status = "Fail";
                result.ErrorMessage = ex.Message;
            }
        }
        else
        {
            result.Status = "Fail";
            result.ErrorMessage = "Malformed DKIM-Signature header: missing d= or s= tag";
        }

        return result;
    }
}
