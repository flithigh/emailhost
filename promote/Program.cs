using System.Text;
using EmailHost.Promote.Data;
using EmailHost.Promote.Endpoints;
using EmailHost.Promote.Entities;
using EmailHost.Promote.Security;
using EmailHost.Promote.Services;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;

namespace EmailHost.Promote;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var builder = WebApplication.CreateBuilder(args);

        // Configuration
        var config = builder.Configuration;
        var providerSetting = config["Database:Provider"] ?? "PostgreSQL";
        var masterPg = config["Database:MasterPostgreSql"] ?? "Host=localhost;Port=5432;Database=flitflap_master;Username=postgres;Password=postgres;";
        var masterSqlite = config["Database:MasterSqlite"] ?? "Data Source=flitflap_master.db";

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

        // Database Context
        if (useSqlite)
        {
            builder.Services.AddDbContext<FlitflapMasterDbContext>(opt => opt.UseSqlite(masterSqlite));
        }
        else
        {
            builder.Services.AddDbContext<FlitflapMasterDbContext>(opt => opt.UseNpgsql(masterPg));
        }

        // Security & Services
        builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        builder.Services.AddSingleton<IDeliverabilityTestService, DeliverabilityTestService>();
        builder.Services.AddScoped<ITenantOnboardingService, TenantOnboardingService>();

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
        });

        var app = builder.Build();

        app.UseCors();
        app.UseDefaultFiles();
        app.UseStaticFiles();

        // Map Endpoints
        app.MapPromoteEndpoints();
        app.MapFallbackToFile("index.html");

        // Seed Master DB if needed
        using (var scope = app.Services.CreateScope())
        {
            var masterDb = scope.ServiceProvider.GetRequiredService<FlitflapMasterDbContext>();
            await masterDb.Database.EnsureCreatedAsync();

            if (!await masterDb.Tenants.AnyAsync())
            {
                var tenant = new Tenant
                {
                    Name = "Flitflap Primary Organization",
                    SubscriptionTier = "Enterprise",
                    BillingStatus = "Active",
                    ContactEmail = "admin@flitflap.com",
                    StorageLimitBytes = 1024L * 1024L * 1024L * 1024L,
                    CreatedAtUtc = DateTime.UtcNow
                };

                tenant.Domains.Add(new DomainRegistry
                {
                    DomainName = "flitflap.com",
                    DatabaseName = "flitflap_tenant_default",
                    IsConnected = true,
                    IsActive = true,
                    VerificationToken = "flitflap-primary-token"
                });

                masterDb.Tenants.Add(tenant);
                await masterDb.SaveChangesAsync();
            }
        }

        // Print Banner
        AnsiConsole.Write(new FigletText("Flitflap Promote").Color(Color.DeepSkyBlue1));
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[bold white]Property[/]");
        table.AddColumn("[bold cyan]Value[/]");
        table.AddRow("Application Scope", "Consumer Tier: Promote & Public Showcase (doc.md Section 2.B.1)");
        table.AddRow("Public Landing URL", "[link=http://localhost:5004]http://localhost:5004[/]");
        table.AddRow("Database Provider", useSqlite ? "[yellow]SQLite Master (Local Fallback)[/]" : "[green]PostgreSQL Master Active[/]");
        table.AddRow("Deliverability Test Tool", "[green]Active (Live DNS MX, SPF, DKIM, DMARC Scanner)[/]");
        table.AddRow("Self-Service Funnel", "[bold yellow]Active (Instant Tenant & Domain Provisioning)[/]");
        AnsiConsole.Write(table);

        await app.RunAsync("http://0.0.0.0:5004");
    }
}

