using System.Security.Cryptography;

namespace EmailHost.ConsoleApp.Security;

public class DkimService
{
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
}

