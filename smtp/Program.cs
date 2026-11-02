using EmailHost.Smtp.Data;
using EmailHost.Smtp.Entities;
using EmailHost.Smtp.Listeners;
using EmailHost.Smtp.Security;
using EmailHost.Smtp.Services;
using EmailHost.Smtp.Storage;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database Configurations (PostgreSQL with SQLite fallback)
var providerSetting = builder.Configuration["Database:Provider"] ?? "PostgreSQL";
var pgMasterConn = builder.Configuration["Database:MasterPostgreSql"] ?? "Host=localhost;Port=5432;Database=flitflap_master;Username=postgres;Password=postgres;";
var pgTenantConn = builder.Configuration["Database:TenantPostgreSql"] ?? "Host=localhost;Port=5432;Database=flitflap_tenant_default;Username=postgres;Password=postgres;";
var sqliteMasterConn = builder.Configuration["Database:MasterSqlite"] ?? "Data Source=flitflap_master.db";
var sqliteTenantConn = builder.Configuration["Database:TenantSqlite"] ?? "Data Source=flitflap_tenant.db";

bool useSqlite = providerSetting.Equals("Sqlite", StringComparison.OrdinalIgnoreCase);

if (!useSqlite)
{
    var testOptions = new DbContextOptionsBuilder<FlitflapMasterDbContext>()
        .UseNpgsql(pgMasterConn)
        .Options;

    using var testDb = new FlitflapMasterDbContext(testOptions);
    try
    {
        if (!await testDb.Database.CanConnectAsync())
        {
            useSqlite = true;
            Console.WriteLine("[WARN] PostgreSQL not available on localhost:5432. Falling back to local SQLite databases.");
        }
    }
    catch
    {
        useSqlite = true;
        Console.WriteLine("[WARN] PostgreSQL check failed. Falling back to local SQLite databases.");
    }
}

if (useSqlite)
{
    builder.Services.AddDbContext<FlitflapMasterDbContext>(options => options.UseSqlite(sqliteMasterConn));
    builder.Services.AddDbContext<FlitflapTenantDbContext>(options => options.UseSqlite(sqliteTenantConn));
}
else
{
    builder.Services.AddDbContext<FlitflapMasterDbContext>(options => options.UseNpgsql(pgMasterConn));
    builder.Services.AddDbContext<FlitflapTenantDbContext>(options => options.UseNpgsql(pgTenantConn));
}

// Storage Service (Garage Object Storage with local fallback)
builder.Services.AddSingleton<IGarageStorageService, GarageStorageService>();

// Security & Compliance Services
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddSingleton<SpooferDetector>();
builder.Services.AddSingleton<AntiSpamFilter>();
builder.Services.AddSingleton<DkimService>();
builder.Services.AddSingleton<DmarcEvaluator>();

var clamHost = builder.Configuration["Smtp:ClamAvHost"] ?? "localhost";
var clamPort = builder.Configuration.GetValue<int>("Smtp:ClamAvPort", 3310);
builder.Services.AddSingleton(new AntiVirusScanner(clamHost, clamPort));

// Message Routing & Delivery
builder.Services.AddSingleton<MessageRoutingService>();

// Concurrent SMTP Background Listeners (Ports 25, 587, 465)
builder.Services.AddHostedService<SmtpListenerService>();

var app = builder.Build();

// Auto initialize databases & seed default tenant
using (var scope = app.Services.CreateScope())
{
    var masterDb = scope.ServiceProvider.GetRequiredService<FlitflapMasterDbContext>();
    var tenantDb = scope.ServiceProvider.GetRequiredService<FlitflapTenantDbContext>();
    await masterDb.Database.EnsureCreatedAsync();
    await tenantDb.Database.EnsureCreatedAsync();

    // Seed default tenant if empty
    if (!await masterDb.Tenants.AnyAsync())
    {
        var defaultTenant = new Tenant
        {
            Name = "Primary Organization",
            Status = "Active",
            SubscriptionTier = "Enterprise"
        };
        masterDb.Tenants.Add(defaultTenant);

        var defaultDomain = new DomainRegistry
        {
            TenantId = defaultTenant.Id,
            DomainName = "flitflap.com",
            IsActive = true,
            DatabaseName = "flitflap_tenant_default"
        };
        masterDb.DomainsRegistry.Add(defaultDomain);
        await masterDb.SaveChangesAsync();

        // Seed default postmaster mailbox & dkim key
        if (!await tenantDb.Mailboxes.AnyAsync())
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var dkimService = scope.ServiceProvider.GetRequiredService<DkimService>();

            var mailbox = new TenantMailbox
            {
                DomainName = "flitflap.com",
                LocalPart = "postmaster",
                Address = "postmaster@flitflap.com",
                PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword("Flitflap@2026", 11),
                QuotaBytes = 20L * 1024L * 1024L * 1024L
            };
            tenantDb.Mailboxes.Add(mailbox);

            var (privKey, pubKey, dnsTxt) = dkimService.GenerateDkimKeyPair("flitflap.com", "default");
            tenantDb.DkimKeys.Add(new DkimKey
            {
                DomainName = "flitflap.com",
                Selector = "default",
                PrivateKeyPem = privKey,
                PublicKeyPem = pubKey,
                DnsTxtRecord = dnsTxt,
                IsActive = true
            });

            await tenantDb.SaveChangesAsync();
        }
    }
}

// -------------------------------------------------------------
// Flitflap Core Mail Engine REST API & Monitoring Endpoints
// -------------------------------------------------------------

app.MapGet("/", (IGarageStorageService garage) => Results.Ok(new
{
    Product = "Flitflap Email Host - Core Mail Engine",
    Architecture = "doc/doc.md Specification Compliant",
    EngineStatus = "Operational",
    SmtpPorts = new
    {
        Port25 = "Server-to-Server Inbound Relay",
        Port587 = "Authenticated Client Submission (STARTTLS)",
        Port465 = "Implicit TLS SMTPS Submission"
    },
    SecurityPipeline = new
    {
        SPF = "Enabled",
        DKIM = "Cryptographic Verification & Automated Signing",
        DMARC = "Policy Enforcement (reject/quarantine/none)",
        AntiSpam = "Bayesian Heuristic Analysis",
        AntiVirus = "Attachment Security + ClamAV Daemon Protocol",
        SpooferDetection = "Enabled"
    },
    Storage = new
    {
        DatabaseModel = "PostgreSQL Dual-Tier (Master + Tenant Sharded)",
        ActiveDatabase = useSqlite ? "SQLite (Local Fallback)" : "PostgreSQL",
        ObjectStore = garage.GetStorageScheme(),
        Bucket = builder.Configuration["Garage:Bucket"] ?? "flitflap-mail-store"
    },
    Endpoints = new[] { "/api/status", "/api/tenants", "/api/domains", "/api/mailboxes", "/api/tracer/{messageId}", "/api/storage/health" }
}));

app.MapGet("/api/status", async (FlitflapMasterDbContext masterDb, FlitflapTenantDbContext tenantDb, IGarageStorageService garage) =>
{
    var tenantsCount = await masterDb.Tenants.CountAsync();
    var domainsCount = await masterDb.DomainsRegistry.CountAsync();
    var mailboxesCount = await tenantDb.Mailboxes.CountAsync();
    var messagesCount = await tenantDb.MessagesIndex.CountAsync();

    return Results.Ok(new
    {
        Status = "Online",
        TimestampUtc = DateTime.UtcNow,
        TenantsHosted = tenantsCount,
        DomainsRegistered = domainsCount,
        ActiveMailboxes = mailboxesCount,
        IndexedMessages = messagesCount,
        StorageEngine = garage.GetStorageScheme(),
        ListeningPorts = new[] { 25, 587, 465 }
    });
});

app.MapGet("/api/tenants", async (FlitflapMasterDbContext masterDb) =>
{
    var tenants = await masterDb.Tenants
        .Include(t => t.Domains)
        .Select(t => new
        {
            t.Id,
            t.Name,
            t.Status,
            t.SubscriptionTier,
            t.BillingStatus,
            DomainsCount = t.Domains.Count,
            t.CreatedAtUtc
        })
        .ToListAsync();

    return Results.Ok(tenants);
});

app.MapGet("/api/domains", async (FlitflapMasterDbContext masterDb) =>
{
    var domains = await masterDb.DomainsRegistry.ToListAsync();
    return Results.Ok(domains);
});

app.MapGet("/api/mailboxes", async (FlitflapTenantDbContext tenantDb) =>
{
    var mailboxes = await tenantDb.Mailboxes
        .Select(m => new
        {
            m.Id,
            m.Address,
            m.IsActive,
            QuotaMb = m.QuotaBytes / (1024 * 1024),
            UsedMb = m.UsedBytes / (1024 * 1024),
            m.CreatedAtUtc
        })
        .ToListAsync();

    return Results.Ok(mailboxes);
});

// Mail Tracer Diagnostic View (doc.md Section 2.A.2)
app.MapGet("/api/tracer/{messageId}", async (string messageId, FlitflapTenantDbContext tenantDb) =>
{
    var traces = await tenantDb.DeliveryTraceLogs
        .Where(t => t.MessageId.Contains(messageId))
        .OrderByDescending(t => t.TimestampUtc)
        .ToListAsync();

    if (!traces.Any())
    {
        return Results.NotFound(new { Error = $"No delivery events found for Message-ID: '{messageId}'" });
    }

    return Results.Ok(traces);
});

app.MapGet("/api/storage/health", async (IGarageStorageService garage) =>
{
    var isHealthy = await garage.CheckHealthAsync();
    return Results.Ok(new
    {
        StorageEngine = garage.GetStorageScheme(),
        S3Connection = isHealthy ? "Connected" : "Offline (Using Local Fallback)",
        TargetBucket = builder.Configuration["Garage:Bucket"] ?? "flitflap-mail-store"
    });
});

app.Run();
