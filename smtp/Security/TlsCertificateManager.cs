using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace EmailHost.Smtp.Security;

public class TlsCertificateManager
{
    private static X509Certificate2? _cachedCert;
    private static readonly object _lock = new();

    public static X509Certificate2 GetOrCreateServerCertificate(string certPath = "", string certPassword = "")
    {
        lock (_lock)
        {
            if (_cachedCert != null) return _cachedCert;

            if (!string.IsNullOrWhiteSpace(certPath) && File.Exists(certPath))
            {
                _cachedCert = X509CertificateLoader.LoadPkcs12FromFile(certPath, certPassword);
                return _cachedCert;
            }

            // Dynamically generate a self-signed RSA TLS certificate for dev/local use
            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest("CN=EmailHost SMTP Server", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
            req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, // Server Authentication
                false));

            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName("localhost");
            sanBuilder.AddDnsName("mail.localhost");
            sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
            req.CertificateExtensions.Add(sanBuilder.Build());

            var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
            var pfxBytes = cert.Export(X509ContentType.Pfx);
            _cachedCert = X509CertificateLoader.LoadPkcs12(pfxBytes, null);
            return _cachedCert;
        }
    }
}
