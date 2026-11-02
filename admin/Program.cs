using System.Text;
using EmailHost.Admin.Data;
using EmailHost.Admin.Endpoints;
using EmailHost.Admin.Entities;
using EmailHost.Admin.Services;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;

namespace EmailHost.Admin;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var builder = WebApplication.CreateBuilder(args);

        // Database Configuration
        var config = builder.Configuration;
        var providerSetting = config["Database:Provider"] ?? "PostgreSQL";
        var masterPg = config["Database:MasterPostgreSql"] ?? "Host=localhost;Port=5432;Database=flitflap_master;Username=postgres;Password=postgres;";
        var tenantPg = config["Database:TenantPostgreSql"] ?? "Host=localhost;Port=5432;Database=flitflap_tenant_default;Username=postgres;Password=postgres;";
        var masterSqlite = config["Database:MasterSqlite"] ?? "Data Source=flitflap_master.db";
        var tenantSqlite = config["Database:TenantSqlite"] ?? "Data Source=flitflap_tenant.db";

        bool useSqlite = providerSetting.Equals("Sqlite", StringComparison.OrdinalIgnoreCase);

        // Pre-check PostgreSQL availability
        if (!useSqlite)
        {
            var testOptions = new DbContextOptionsBuilder<FlitflapMasterDbContext>()
                .UseNpgsql(masterPg)
                .Options;

            using var testDb = new FlitflapMasterDbContext(testOptions);
            try
            {
                if (!await testDb.Database.CanConnectAsync())
                {
                    useSqlite = true;
                }
            }
            catch
            {
                useSqlite = true;
            }
        }

        // Database Contexts
        if (useSqlite)
        {
            builder.Services.AddDbContext<FlitflapMasterDbContext>(opt => opt.UseSqlite(masterSqlite));
            builder.Services.AddDbContext<FlitflapTenantDbContext>(opt => opt.UseSqlite(tenantSqlite));
        }
        else
        {
            builder.Services.AddDbContext<FlitflapMasterDbContext>(opt => opt.UseNpgsql(masterPg));
            builder.Services.AddDbContext<FlitflapTenantDbContext>(opt => opt.UseNpgsql(tenantPg));
        }

        // Services
        builder.Services.AddHttpClient();
        builder.Services.AddScoped<ITenantLifecycleService, TenantLifecycleService>();
        builder.Services.AddScoped<IInfrastructureMonitorService, InfrastructureMonitorService>();
        builder.Services.AddScoped<IIpPoolGovernanceService, IpPoolGovernanceService>();
        builder.Services.AddScoped<ISecurityAbuseService, SecurityAbuseService>();

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
        });

        var app = builder.Build();

        app.UseCors();
        app.UseDefaultFiles();
        app.UseStaticFiles();

        // Map Endpoints
        app.MapAdminEndpoints();
        app.MapFallbackToFile("index.html");

        // Seed Master DB if needed
        using (var scope = app.Services.CreateScope())
        {
            var masterDb = scope.ServiceProvider.GetRequiredService<FlitflapMasterDbContext>();
            await masterDb.Database.EnsureCreatedAsync();

            if (!await masterDb.Tenants.AnyAsync())
            {
                var primaryTenant = new Tenant
                {
                    Name = "Flitflap Primary Organization",
                    SubscriptionTier = "Enterprise",
                    BillingStatus = "Active",
                    ContactEmail = "admin@flitflap.com",
                    StorageLimitBytes = 1024L * 1024L * 1024L * 1024L, // 1 TB
                    CreatedAtUtc = DateTime.UtcNow
                };

                primaryTenant.Domains.Add(new DomainRegistry
                {
                    DomainName = "flitflap.com",
                    DatabaseName = "flitflap_tenant_default",
                    IsConnected = true,
                    IsActive = true,
                    VerificationToken = "flitflap-primary-token"
                });

                masterDb.Tenants.Add(primaryTenant);

                var starterTenant = new Tenant
                {
                    Name = "Acme Global Solutions",
                    SubscriptionTier = "Pro",
                    BillingStatus = "Active",
                    ContactEmail = "it@acmesolutions.com",
                    StorageLimitBytes = 250L * 1024L * 1024L * 1024L,
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-14)
                };

                starterTenant.Domains.Add(new DomainRegistry
                {
                    DomainName = "acmesolutions.com",
                    DatabaseName = "domain-database-acmesolutions-com",
                    IsConnected = true,
                    IsActive = true,
                    VerificationToken = "acme-verify-token-123"
                });

                masterDb.Tenants.Add(starterTenant);

                // Default IP Pools
                masterDb.IpPools.AddRange(
                    new IpPool
                    {
                        IpAddress = "198.51.100.10",
                        Hostname = "mail-out-01.flitflap.com",
                        IsWarm = true,
                        WarmupStage = 3,
                        ReputationScore = 99.8,
                        IsBlacklisted = false,
                        BlacklistDetails = "Clean (Spamhaus, Barracuda, SORBS)",
                        AssignedTenantName = "Global Shared Pool"
                    },
                    new IpPool
                    {
                        IpAddress = "198.51.100.11",
                        Hostname = "mail-out-02.flitflap.com",
                        IsWarm = true,
                        WarmupStage = 3,
                        ReputationScore = 98.5,
                        IsBlacklisted = false,
                        BlacklistDetails = "Clean (Spamhaus, Barracuda, SORBS)",
                        AssignedTenantName = "Global Shared Pool"
                    },
                    new IpPool
                    {
                        IpAddress = "198.51.100.25",
                        Hostname = "dedicated-relay-01.flitflap.com",
                        IsWarm = false,
                        WarmupStage = 2,
                        ReputationScore = 94.0,
                        IsBlacklisted = false,
                        BlacklistDetails = "Warmup schedule stage 2 (50k msgs/day)",
                        AssignedTenantName = "Acme Global Solutions"
                    }
                );

                // Default Abuse Rules
                masterDb.GlobalAbuseRules.AddRange(
                    new GlobalAbuseRule
                    {
                        RuleType = "TldBlacklist",
                        Pattern = ".click",
                        Reason = "Excessive spam and phishing rate"
                    },
                    new GlobalAbuseRule
                    {
                        RuleType = "DomainBlacklist",
                        Pattern = "*.malicious-inbound-trap.com",
                        Reason = "Known botnet relay sender"
                    }
                );

                // Initial Staff Audit Log
                masterDb.StaffAuditLogs.Add(new StaffAuditLog
                {
                    StaffUser = "system@flitflap.com",
                    Action = "ClusterInitialized",
                    TargetTenantOrDomain = "flitflap_master",
                    Details = "Super Admin cluster initialized with primary enterprise organization and outbound IP pools."
                });

                await masterDb.SaveChangesAsync();
            }
        }

        // Print Banner
        AnsiConsole.Write(new FigletText("Flitflap Super Admin").Color(Color.DeepSkyBlue1));
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[bold white]Property[/]");
        table.AddColumn("[bold cyan]Value[/]");
        table.AddRow("Application Tier", "Operator Scope: Super Admin Application (doc.md Section 2.A.1)");
        table.AddRow("Admin Web Console", "[link=http://localhost:5001]http://localhost:5001[/]");
        table.AddRow("Database Provider", useSqlite ? "[yellow]Dual-Tier SQLite (Local Fallback)[/]" : "[green]PostgreSQL Master DB Active[/]");
        table.AddRow("IP Pools Governance", "[green]3 Active Relays (Warm-Up & RBL Monitor Active)[/]");
        table.AddRow("Tenant Governance", "[bold yellow]Lifecycle & Storage Management Active[/]");
        AnsiConsole.Write(table);

        await app.RunAsync("http://0.0.0.0:5001");
    }
}

