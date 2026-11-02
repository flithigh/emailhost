using System.Net.Sockets;
using System.Text;
using MimeKit;

namespace EmailHost.Smtp.Security;

public class VirusScanResult
{
    public bool IsInfected { get; set; }
    public string ThreatName { get; set; } = string.Empty;
    public string DetectionMethod { get; set; } = string.Empty; // "ClamAV" or "Heuristic"
}

public class AntiVirusScanner
{
    private static readonly HashSet<string> DangerousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".scr", ".vbs", ".bat", ".cmd", ".ps1", ".pif", ".com",
        ".hta", ".cpl", ".msi", ".msp", ".reg", ".wsf", ".wsh", ".iso"
    };

    private readonly string _clamAvHost;
    private readonly int _clamAvPort;

    public AntiVirusScanner(string clamAvHost = "localhost", int clamAvPort = 3310)
    {
        _clamAvHost = clamAvHost;
        _clamAvPort = clamAvPort;
    }

    public async Task<VirusScanResult> ScanMessageAsync(MimeMessage message, byte[] rawBytes)
    {
        // 1. Try ClamAV daemon first if running
        var clamResult = await TryClamAvScanAsync(rawBytes);
        if (clamResult != null)
        {
            return clamResult;
        }

        // 2. Fallback to Heuristic Attachment Scanner
        return ScanAttachmentsHeuristic(message);
    }

    private VirusScanResult ScanAttachmentsHeuristic(MimeMessage message)
    {
        var result = new VirusScanResult { DetectionMethod = "Heuristic Extension Analysis" };

        foreach (var attachment in message.Attachments)
        {
            var fileName = attachment.ContentDisposition?.FileName
                           ?? (attachment as MimePart)?.FileName
                           ?? string.Empty;

            if (string.IsNullOrWhiteSpace(fileName)) continue;

            var ext = Path.GetExtension(fileName);
            if (DangerousExtensions.Contains(ext))
            {
                result.IsInfected = true;
                result.ThreatName = $"Suspicious executable attachment detected: '{fileName}'";
                return result;
            }

            // Check for deceptive double extensions (e.g. statement.pdf.exe)
            var lower = fileName.ToLowerInvariant();
            if (lower.EndsWith(".pdf.exe") || lower.EndsWith(".docx.exe") || lower.EndsWith(".xlsx.vbs") || lower.EndsWith(".zip.scr"))
            {
                result.IsInfected = true;
                result.ThreatName = $"Deceptive double-extension malware: '{fileName}'";
                return result;
            }
        }

        return result;
    }

    private async Task<VirusScanResult?> TryClamAvScanAsync(byte[] rawBytes)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(_clamAvHost, _clamAvPort);
            if (await Task.WhenAny(connectTask, Task.Delay(500)) != connectTask)
            {
                return null; // ClamAV daemon not running or not responding
            }

            using var stream = client.GetStream();
            var command = Encoding.ASCII.GetBytes("zINSTREAM\0");
            await stream.WriteAsync(command);

            // Stream size in big-endian + data
            var sizeBytes = BitConverter.GetBytes((uint)rawBytes.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(sizeBytes);

            await stream.WriteAsync(sizeBytes);
            await stream.WriteAsync(rawBytes);
            await stream.WriteAsync(new byte[] { 0, 0, 0, 0 }); // 0-length chunk indicates EOF

            var responseBuffer = new byte[256];
            int read = await stream.ReadAsync(responseBuffer);
            var response = Encoding.ASCII.GetString(responseBuffer, 0, read);

            if (response.Contains("FOUND"))
            {
                return new VirusScanResult
                {
                    IsInfected = true,
                    ThreatName = response.Trim(),
                    DetectionMethod = "ClamAV TCP Daemon"
                };
            }

            return new VirusScanResult
            {
                IsInfected = false,
                DetectionMethod = "ClamAV TCP Daemon"
            };
        }
        catch
        {
            return null; // ClamAV unavailable, fallback to heuristic
        }
    }
}

