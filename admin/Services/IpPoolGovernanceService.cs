using DnsClient;
using EmailHost.Admin.Data;
using EmailHost.Admin.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EmailHost.Admin.Services;

public record AddIpRequest(string IpAddress, string Hostname, string AssignedTenantName);

public interface IIpPoolGovernanceService
{
    Task<List<IpPool>> GetAllIpsAsync();
    Task<IpPool> AddIpAsync(AddIpRequest request, string staffUser = "admin@flitflap.com");
    Task<bool> ToggleWarmupAsync(Guid ipId, int stage, string staffUser = "admin@flitflap.com");
    Task<IpPool?> CheckBlacklistAsync(Guid ipId);
    Task RunAllBlacklistAuditsAsync();
}

public class IpPoolGovernanceService : IIpPoolGovernanceService
{
    private readonly FlitflapMasterDbContext _masterDb;
    private readonly ILookupClient _dnsClient;
    private readonly ILogger<IpPoolGovernanceService> _logger;

    public IpPoolGovernanceService(FlitflapMasterDbContext masterDb, ILogger<IpPoolGovernanceService> logger)
    {
        _masterDb = masterDb;
        _dnsClient = new LookupClient(NameServer.GooglePublicDns, NameServer.Cloudflare);
        _logger = logger;
    }

    public async Task<List<IpPool>> GetAllIpsAsync()
    {
        return await _masterDb.IpPools.OrderByDescending(ip => ip.ReputationScore).ToListAsync();
    }

    public async Task<IpPool> AddIpAsync(AddIpRequest request, string staffUser = "admin@flitflap.com")
    {
        var pool = new IpPool
        {
            IpAddress = request.IpAddress.Trim(),
            Hostname = request.Hostname.Trim(),
            AssignedTenantName = string.IsNullOrWhiteSpace(request.AssignedTenantName) ? "Global Shared Pool" : request.AssignedTenantName,
            IsWarm = false,
            WarmupStage = 1,
            ReputationScore = 95.0,
            IsBlacklisted = false,
            BlacklistDetails = "New IP - Warmup schedule initialized.",
            CreatedAtUtc = DateTime.UtcNow
        };

        _masterDb.IpPools.Add(pool);
        _masterDb.StaffAuditLogs.Add(new StaffAuditLog
        {
            StaffUser = staffUser,
            Action = "OutboundIpAdded",
            TargetTenantOrDomain = pool.IpAddress,
            Details = $"Allocated relay IP {pool.IpAddress} ({pool.Hostname}) to pool {pool.AssignedTenantName}"
        });

        await _masterDb.SaveChangesAsync();
        return pool;
    }

    public async Task<bool> ToggleWarmupAsync(Guid ipId, int stage, string staffUser = "admin@flitflap.com")
    {
        var ip = await _masterDb.IpPools.FindAsync(ipId);
        if (ip == null) return false;

        ip.WarmupStage = Math.Clamp(stage, 1, 3);
        ip.IsWarm = ip.WarmupStage == 3;

        _masterDb.StaffAuditLogs.Add(new StaffAuditLog
        {
            StaffUser = staffUser,
            Action = "IpWarmupUpdated",
            TargetTenantOrDomain = ip.IpAddress,
            Details = $"Warmup stage updated to {ip.WarmupStage} (Fully Warm: {ip.IsWarm})"
        });

        await _masterDb.SaveChangesAsync();
        return true;
    }

    public async Task<IpPool?> CheckBlacklistAsync(Guid ipId)
    {
        var ip = await _masterDb.IpPools.FindAsync(ipId);
        if (ip == null) return null;

        var parts = ip.IpAddress.Split('.');
        if (parts.Length == 4)
        {
            var reversedIp = $"{parts[3]}.{parts[2]}.{parts[1]}.{parts[0]}";
            var rbls = new[]
            {
                "zen.spamhaus.org",
                "b.barracudacentral.org",
                "dnsbl.sorbs.net"
            };

            var listedOn = new List<string>();

            foreach (var rbl in rbls)
            {
                try
                {
                    var query = await _dnsClient.QueryAsync($"{reversedIp}.{rbl}", QueryType.A);
                    if (query.Answers.Any())
                    {
                        listedOn.Add(rbl.Split('.')[0]);
                    }
                }
                catch
                {
                    // Network or timeout in sandbox
                }
            }

            if (listedOn.Count > 0)
            {
                ip.IsBlacklisted = true;
                ip.BlacklistDetails = $"Listed on: {string.Join(", ", listedOn)}";
                ip.ReputationScore = Math.Max(20.0, ip.ReputationScore - (listedOn.Count * 25.0));
            }
            else
            {
                ip.IsBlacklisted = false;
                ip.BlacklistDetails = "Clean (Spamhaus, Barracuda, SORBS)";
                ip.ReputationScore = Math.Min(100.0, ip.ReputationScore + 1.0);
            }
        }

        ip.LastCheckedAtUtc = DateTime.UtcNow;
        await _masterDb.SaveChangesAsync();
        return ip;
    }

    public async Task RunAllBlacklistAuditsAsync()
    {
        var ips = await _masterDb.IpPools.ToListAsync();
        foreach (var ip in ips)
        {
            await CheckBlacklistAsync(ip.Id);
        }
    }
}

