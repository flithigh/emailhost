using Amazon.S3;
using EmailHost.Admin.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EmailHost.Admin.Services;

public record InfrastructureStatusDto(
    bool MasterDbHealthy,
    string MasterDbProvider,
    int TotalTenants,
    int TotalDomains,
    bool TenantDbHealthy,
    int TotalMailboxes,
    int TotalMessages,
    bool GarageS3Healthy,
    string GarageBucket,
    string GarageEndpoint,
    bool SmtpEngineHealthy,
    string SmtpEngineStatus,
    long SystemWideSizeBytes,
    DateTime CheckedAtUtc
);

public interface IInfrastructureMonitorService
{
    Task<InfrastructureStatusDto> GetClusterHealthAsync();
}

public class InfrastructureMonitorService : IInfrastructureMonitorService
{
    private readonly FlitflapMasterDbContext _masterDb;
    private readonly FlitflapTenantDbContext _tenantDb;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<InfrastructureMonitorService> _logger;

    public InfrastructureMonitorService(
        FlitflapMasterDbContext masterDb,
        FlitflapTenantDbContext tenantDb,
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<InfrastructureMonitorService> logger)
    {
        _masterDb = masterDb;
        _tenantDb = tenantDb;
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<InfrastructureStatusDto> GetClusterHealthAsync()
    {
        bool masterDbOk = false;
        int tenants = 0;
        int domains = 0;

        try
        {
            masterDbOk = await _masterDb.Database.CanConnectAsync();
            if (masterDbOk)
            {
                tenants = await _masterDb.Tenants.CountAsync();
                domains = await _masterDb.DomainsRegistry.CountAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Master DB health check failed: {Error}", ex.Message);
        }

        bool tenantDbOk = false;
        int mailboxes = 0;
        int messages = 0;
        long totalBytes = 0;

        try
        {
            tenantDbOk = await _tenantDb.Database.CanConnectAsync();
            if (tenantDbOk)
            {
                mailboxes = await _tenantDb.Mailboxes.CountAsync();
                messages = await _tenantDb.MessagesIndex.CountAsync();
                totalBytes = await _tenantDb.Mailboxes.SumAsync(m => m.UsedBytes);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Tenant DB health check note: {Error}", ex.Message);
        }

        // Check Garage S3
        bool garageOk = false;
        var garageBucket = _config["Garage:Bucket"] ?? "flitflap-mail-store";
        var garageEndpoint = _config["Garage:Endpoint"] ?? "http://localhost:3900";

        try
        {
            var accessKey = _config["Garage:AccessKey"] ?? "garage_access_key";
            var secretKey = _config["Garage:SecretKey"] ?? "garage_secret_key";
            var s3Config = new AmazonS3Config
            {
                ServiceURL = garageEndpoint,
                ForcePathStyle = true,
                AuthenticationRegion = _config["Garage:Region"] ?? "garage",
                Timeout = TimeSpan.FromSeconds(3)
            };
            using var client = new AmazonS3Client(accessKey, secretKey, s3Config);
            await client.ListBucketsAsync();
            garageOk = true;
        }
        catch
        {
            garageOk = false;
        }

        // Check SMTP Engine Daemon
        bool smtpOk = false;
        string smtpStatus = "Offline / Standby";
        var statusUrl = _config["SmtpEngine:StatusUrl"] ?? "http://localhost:5000/api/status";

        try
        {
            var http = _httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(2);
            var res = await http.GetAsync(statusUrl);
            if (res.IsSuccessStatusCode)
            {
                smtpOk = true;
                smtpStatus = "Listening on Ports 25, 587, 465";
            }
        }
        catch
        {
            smtpOk = false;
            smtpStatus = "SMTP service offline on localhost:5000";
        }

        return new InfrastructureStatusDto(
            MasterDbHealthy: masterDbOk,
            MasterDbProvider: _masterDb.Database.ProviderName?.Contains("Sqlite") == true ? "SQLite (Local Dev)" : "PostgreSQL Cluster",
            TotalTenants: tenants,
            TotalDomains: domains,
            TenantDbHealthy: tenantDbOk,
            TotalMailboxes: mailboxes,
            TotalMessages: messages,
            GarageS3Healthy: garageOk,
            GarageBucket: garageBucket,
            GarageEndpoint: garageEndpoint,
            SmtpEngineHealthy: smtpOk,
            SmtpEngineStatus: smtpStatus,
            SystemWideSizeBytes: totalBytes,
            CheckedAtUtc: DateTime.UtcNow
        );
    }
}

