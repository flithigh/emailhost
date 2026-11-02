using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using EmailHost.Smtp.Data;
using EmailHost.Smtp.Entities;
using EmailHost.Smtp.Security;
using EmailHost.Smtp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EmailHost.Smtp.Handlers;

public class SmtpSessionHandler
{
    private readonly TcpClient _client;
    private readonly int _listeningPort;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;
    private readonly X509Certificate2 _serverCertificate;
    private readonly long _maxMessageSize;

    public SmtpSessionHandler(
        TcpClient client,
        int listeningPort,
        IServiceProvider serviceProvider,
        ILogger logger,
        X509Certificate2 serverCertificate,
        long maxMessageSize = 25 * 1024 * 1024)
    {
        _client = client;
        _listeningPort = listeningPort;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _serverCertificate = serverCertificate;
        _maxMessageSize = maxMessageSize;
    }

    public async Task ProcessSessionAsync(CancellationToken cancellationToken)
    {
        var clientEndpoint = _client.Client.RemoteEndPoint?.ToString() ?? "unknown";
        var clientIp = clientEndpoint.Split(':')[0];
        _logger.LogInformation("SMTP connection established from {Client} on port {Port}", clientEndpoint, _listeningPort);

        Stream networkStream = _client.GetStream();
        bool isTls = false;

        // Port 465 is SMTPS (Implicit SSL): authenticate before greeting
        if (_listeningPort == 465)
        {
            var sslStream = new SslStream(networkStream, false);
            try
            {
                await sslStream.AuthenticateAsServerAsync(_serverCertificate, false, SslProtocols.Tls12 | SslProtocols.Tls13, false);
                networkStream = sslStream;
                isTls = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("SSL Handshake failed on port 465 from {Client}: {Error}", clientEndpoint, ex.Message);
                return;
            }
        }

        using var reader = new StreamReader(networkStream, Encoding.ASCII, false, 4096, leaveOpen: true);
        using var writer = new StreamWriter(networkStream, Encoding.ASCII, 4096, leaveOpen: true) { AutoFlush = true };

        // 220 Service Ready Banner (Flitflap Core Mail Engine)
        await writer.WriteLineAsync("220 mail.flitflap.com Flitflap Core Mail Engine Ready (ASP.NET Core 9)");

        string clientHelo = string.Empty;
        bool isAuthenticated = false;
        string authenticatedUser = string.Empty;
        string currentSender = string.Empty;
        string spfVerdict = "None";
        string spfDomain = string.Empty;
        var recipients = new List<string>();

        while (!cancellationToken.IsCancellationRequested && _client.Connected)
        {
            string? line = null;
            try
            {
                line = await reader.ReadLineAsync(cancellationToken);
            }
            catch
            {
                break;
            }

            if (line == null) break;

            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].ToUpperInvariant();
            var argument = parts.Length > 1 ? parts[1].Trim() : string.Empty;

            switch (command)
            {
                case "HELO":
                    clientHelo = argument;
                    await writer.WriteLineAsync($"250 mail.flitflap.com Hello {clientHelo}");
                    break;

                case "EHLO":
                    clientHelo = argument;
                    await writer.WriteLineAsync("250-mail.flitflap.com Hello " + clientHelo);
                    await writer.WriteLineAsync($"250-SIZE {_maxMessageSize}");
                    await writer.WriteLineAsync("250-8BITMIME");
                    if (!isTls && _listeningPort != 465)
                    {
                        await writer.WriteLineAsync("250-STARTTLS");
                    }
                    await writer.WriteLineAsync("250-AUTH LOGIN PLAIN");
                    await writer.WriteLineAsync("250 OK");
                    break;

                case "STARTTLS":
                    if (isTls)
                    {
                        await writer.WriteLineAsync("503 5.5.1 Already in TLS mode");
                        break;
                    }
                    await writer.WriteLineAsync("220 2.0.0 Ready to start TLS");
                    var startTlsStream = new SslStream(networkStream, false);
                    try
                    {
                        await startTlsStream.AuthenticateAsServerAsync(_serverCertificate, false, SslProtocols.Tls12 | SslProtocols.Tls13, false);
                        networkStream = startTlsStream;
                        isTls = true;
                        clientHelo = string.Empty;
                        isAuthenticated = false;
                        authenticatedUser = string.Empty;
                        currentSender = string.Empty;
                        recipients.Clear();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("STARTTLS handshake error from {Client}: {Error}", clientEndpoint, ex.Message);
                        return;
                    }
                    break;

                case "AUTH":
                    if (isAuthenticated)
                    {
                        await writer.WriteLineAsync("503 5.5.1 Already authenticated");
                        break;
                    }

                    var authResult = await HandleAuthAsync(argument, reader, writer, clientIp);
                    if (authResult.Success)
                    {
                        isAuthenticated = true;
                        authenticatedUser = authResult.Username;
                        await writer.WriteLineAsync("235 2.7.0 Authentication successful");
                    }
                    else
                    {
                        await writer.WriteLineAsync($"535 5.7.8 Authentication credentials invalid: {authResult.Error}");
                    }
                    break;

                case "MAIL":
                    if (!argument.StartsWith("FROM:", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("501 5.5.4 Syntax: MAIL FROM:<address>");
                        break;
                    }

                    var fromAddr = ExtractEmailAddress(argument.Substring(5));
                    if (string.IsNullOrWhiteSpace(fromAddr))
                    {
                        await writer.WriteLineAsync("501 5.1.7 Bad sender address syntax");
                        break;
                    }

                    // Spoofer Detection Check
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var masterDb = scope.ServiceProvider.GetRequiredService<FlitflapMasterDbContext>();
                        var spooferDetector = scope.ServiceProvider.GetRequiredService<SpooferDetector>();

                        var spoofCheck = await spooferDetector.CheckSenderAsync(fromAddr, clientIp, isAuthenticated, masterDb);
                        if (spoofCheck.IsSpoofed)
                        {
                            _logger.LogWarning("Spoofing attempt from {Ip}: {Reason}", clientIp, spoofCheck.Reason);
                            await writer.WriteLineAsync($"550 5.7.1 Sender rejected: {spoofCheck.Reason}");
                            break;
                        }
                        spfVerdict = spoofCheck.SpfVerdict;
                        spfDomain = spoofCheck.SenderDomain;
                    }

                    currentSender = fromAddr;
                    recipients.Clear();
                    await writer.WriteLineAsync("250 2.1.0 Sender OK");
                    break;

                case "RCPT":
                    if (string.IsNullOrEmpty(currentSender))
                    {
                        await writer.WriteLineAsync("503 5.5.1 Error: need MAIL command first");
                        break;
                    }

                    if (!argument.StartsWith("TO:", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("501 5.5.4 Syntax: RCPT TO:<address>");
                        break;
                    }

                    var toAddr = ExtractEmailAddress(argument.Substring(3));
                    if (string.IsNullOrWhiteSpace(toAddr))
                    {
                        await writer.WriteLineAsync("501 5.1.3 Bad recipient address syntax");
                        break;
                    }

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var masterDb = scope.ServiceProvider.GetRequiredService<FlitflapMasterDbContext>();
                        var tenantDb = scope.ServiceProvider.GetRequiredService<FlitflapTenantDbContext>();

                        var toParts = toAddr.Split('@');
                        var toDomain = toParts.Length == 2 ? toParts[1].ToLowerInvariant() : "";
                        var isLocalDomain = await masterDb.DomainsRegistry.AnyAsync(d => d.DomainName.ToLower() == toDomain && d.IsActive);

                        if (!isLocalDomain && !isAuthenticated)
                        {
                            await writer.WriteLineAsync("550 5.7.1 Relaying denied: Authentication required for external destinations");
                            break;
                        }

                        if (isLocalDomain)
                        {
                            var mailboxExists = await tenantDb.Mailboxes.AnyAsync(m => m.Address.ToLower() == toAddr.ToLower() && m.IsActive);
                            var aliasExists = !mailboxExists && await tenantDb.Aliases.AnyAsync(a => a.SourceAlias.ToLower() == toAddr.ToLower() && a.IsActive);

                            if (!mailboxExists && !aliasExists)
                            {
                                await writer.WriteLineAsync($"550 5.1.1 User unknown: <{toAddr}>");
                                break;
                            }
                        }
                    }

                    recipients.Add(toAddr);
                    await writer.WriteLineAsync("250 2.1.5 Recipient OK");
                    break;

                case "DATA":
                    if (string.IsNullOrEmpty(currentSender) || recipients.Count == 0)
                    {
                        await writer.WriteLineAsync("503 5.5.1 Error: need valid MAIL and RCPT commands first");
                        break;
                    }

                    await writer.WriteLineAsync("354 Start mail input; end with <CRLF>.<CRLF>");

                    using (var memoryStream = new MemoryStream())
                    {
                        bool messageAccepted = true;

                        while (true)
                        {
                            var dataLine = await reader.ReadLineAsync(cancellationToken);
                            if (dataLine == null) break;

                            if (dataLine == ".") break;

                            if (dataLine.StartsWith(".."))
                            {
                                dataLine = dataLine.Substring(1);
                            }

                            var bytes = Encoding.UTF8.GetBytes(dataLine + "\r\n");
                            if (memoryStream.Length + bytes.Length > _maxMessageSize)
                            {
                                messageAccepted = false;
                                break;
                            }
                            await memoryStream.WriteAsync(bytes);
                        }

                        if (!messageAccepted)
                        {
                            await writer.WriteLineAsync($"552 5.3.4 Message size exceeds maximum limit ({_maxMessageSize / (1024 * 1024)} MB)");
                            break;
                        }

                        var rawBytes = memoryStream.ToArray();
                        using (var scope = _serviceProvider.CreateScope())
                        {
                            var masterDb = scope.ServiceProvider.GetRequiredService<FlitflapMasterDbContext>();
                            var tenantDb = scope.ServiceProvider.GetRequiredService<FlitflapTenantDbContext>();
                            var router = scope.ServiceProvider.GetRequiredService<MessageRoutingService>();

                            bool anyFailed = false;
                            string lastMsg = "";

                            foreach (var recipient in recipients)
                            {
                                var result = await router.RouteAndDeliverAsync(
                                    currentSender,
                                    recipient,
                                    rawBytes,
                                    clientIp,
                                    spfVerdict,
                                    spfDomain,
                                    isAuthenticated,
                                    masterDb,
                                    tenantDb
                                );

                                if (!result.Success)
                                {
                                    anyFailed = true;
                                    lastMsg = result.Message;
                                }
                            }

                            if (!anyFailed)
                            {
                                await writer.WriteLineAsync("250 2.0.0 OK: Message accepted for delivery");
                            }
                            else
                            {
                                await writer.WriteLineAsync(lastMsg);
                            }
                        }

                        currentSender = string.Empty;
                        recipients.Clear();
                    }
                    break;

                case "RSET":
                    currentSender = string.Empty;
                    recipients.Clear();
                    await writer.WriteLineAsync("250 2.0.0 OK Reset state");
                    break;

                case "NOOP":
                    await writer.WriteLineAsync("250 2.0.0 OK");
                    break;

                case "QUIT":
                    await writer.WriteLineAsync("221 2.0.0 Flitflap Core Mail Engine closing transmission channel");
                    return;

                default:
                    await writer.WriteLineAsync($"502 5.5.2 Command not implemented: {command}");
                    break;
            }
        }
    }

    private async Task<(bool Success, string Username, string Error)> HandleAuthAsync(
        string argument,
        StreamReader reader,
        StreamWriter writer,
        string clientIp)
    {
        var authParts = argument.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var mechanism = authParts[0].ToUpperInvariant();

        string username = "";
        string password = "";

        if (mechanism == "LOGIN")
        {
            if (authParts.Length > 1)
            {
                username = DecodeBase64(authParts[1]);
            }
            else
            {
                await writer.WriteLineAsync("334 VXNlcm5hbWU6"); // "Username:" in Base64
                var userB64 = await reader.ReadLineAsync();
                username = DecodeBase64(userB64 ?? "");
            }

            await writer.WriteLineAsync("334 UGFzc3dvcmQ6"); // "Password:" in Base64
            var passB64 = await reader.ReadLineAsync();
            password = DecodeBase64(passB64 ?? "");
        }
        else if (mechanism == "PLAIN")
        {
            string payloadB64 = authParts.Length > 1 ? authParts[1] : "";
            if (string.IsNullOrEmpty(payloadB64))
            {
                await writer.WriteLineAsync("334 ");
                payloadB64 = await reader.ReadLineAsync() ?? "";
            }

            try
            {
                var decodedBytes = Convert.FromBase64String(payloadB64);
                var nullIndices = new List<int>();
                for (int i = 0; i < decodedBytes.Length; i++)
                {
                    if (decodedBytes[i] == 0) nullIndices.Add(i);
                }

                if (nullIndices.Count >= 2)
                {
                    username = Encoding.UTF8.GetString(decodedBytes, nullIndices[0] + 1, nullIndices[1] - nullIndices[0] - 1);
                    password = Encoding.UTF8.GetString(decodedBytes, nullIndices[1] + 1, decodedBytes.Length - nullIndices[1] - 1);
                }
            }
            catch
            {
                return (false, "", "Malformed SASL PLAIN payload");
            }
        }
        else
        {
            return (false, "", $"Unsupported authentication mechanism: {mechanism}");
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return (false, "", "Missing credentials");
        }

        using var scope = _serviceProvider.CreateScope();
        var tenantDb = scope.ServiceProvider.GetRequiredService<FlitflapTenantDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var normalizedUser = username.Trim().ToLowerInvariant();
        var mailbox = await tenantDb.Mailboxes.FirstOrDefaultAsync(m => m.Address.ToLower() == normalizedUser || m.LocalPart.ToLower() == normalizedUser);

        if (mailbox == null || !mailbox.IsActive || !hasher.VerifyPassword(password, mailbox.PasswordHash))
        {
            return (false, "", "Invalid username or password");
        }

        return (true, mailbox.Address, "");
    }

    private static string ExtractEmailAddress(string raw)
    {
        var start = raw.IndexOf('<');
        var end = raw.IndexOf('>');
        if (start >= 0 && end > start)
        {
            return raw.Substring(start + 1, end - start - 1).Trim();
        }
        return raw.Trim();
    }

    private static string DecodeBase64(string base64)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64.Trim());
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }
}
