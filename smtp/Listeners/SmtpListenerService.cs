using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using EmailHost.Smtp.Handlers;
using EmailHost.Smtp.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EmailHost.Smtp.Listeners;

public class SmtpListenerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SmtpListenerService> _logger;
    private readonly IConfiguration _config;
    private readonly X509Certificate2 _certificate;

    private readonly List<int> _ports = new();
    private readonly List<TcpListener> _listeners = new();

    public SmtpListenerService(
        IServiceProvider serviceProvider,
        ILogger<SmtpListenerService> logger,
        IConfiguration config)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config;

        var certPath = _config["Smtp:TlsCertificatePath"] ?? "";
        var certPass = _config["Smtp:TlsCertificatePassword"] ?? "";
        _certificate = TlsCertificateManager.GetOrCreateServerCertificate(certPath, certPass);

        int p25 = _config.GetValue<int>("Smtp:Port25", 25);
        int p587 = _config.GetValue<int>("Smtp:Port587", 587);
        int p465 = _config.GetValue<int>("Smtp:Port465", 465);

        _ports.Add(p25);
        _ports.Add(p587);
        _ports.Add(p465);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tasks = new List<Task>();

        foreach (var port in _ports)
        {
            var listener = TryStartListener(port);
            if (listener != null)
            {
                _listeners.Add(listener);
                tasks.Add(ListenLoopAsync(listener, port, stoppingToken));
            }
        }

        if (tasks.Count == 0)
        {
            _logger.LogError("Could not start any SMTP listener ports. Check permissions and port availability.");
            return;
        }

        _logger.LogInformation("SMTP Server is running and listening on {Count} port(s).", tasks.Count);
        await Task.WhenAll(tasks);
    }

    private TcpListener? TryStartListener(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            _logger.LogInformation("✔ SMTP Listener successfully started on 0.0.0.0:{Port}", port);
            return listener;
        }
        catch (SocketException ex)
        {
            _logger.LogWarning("Could not bind port {Port}: {Error}. (If port 25 requires Admin rights on Windows, run as Administrator or configure high port)", port, ex.Message);
            return null;
        }
    }

    private async Task ListenLoopAsync(TcpListener listener, int port, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(stoppingToken);

                // Handle session in background task so other clients are not blocked
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var maxBytes = _config.GetValue<long>("Smtp:MaxMessageSizeBytes", 25 * 1024 * 1024);
                        var handler = new SmtpSessionHandler(
                            client,
                            port,
                            _serviceProvider,
                            _logger,
                            _certificate,
                            maxBytes
                        );
                        await handler.ProcessSessionAsync(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling SMTP connection on port {Port}", port);
                    }
                    finally
                    {
                        try { client.Dispose(); } catch { }
                    }
                }, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in AcceptTcpClientAsync for port {Port}", port);
            }
        }

        try
        {
            listener.Stop();
        }
        catch { }
    }

    public override void Dispose()
    {
        foreach (var listener in _listeners)
        {
            try { listener.Stop(); } catch { }
        }
        base.Dispose();
    }
}

