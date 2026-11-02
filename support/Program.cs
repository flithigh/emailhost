using System.Text;
using EmailHost.Support.Data;
using EmailHost.Support.Endpoints;
using EmailHost.Support.Entities;
using EmailHost.Support.Services;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;

namespace EmailHost.Support;

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
        builder.Services.AddScoped<IMailTracerDiagnosticService, MailTracerDiagnosticService>();
        builder.Services.AddScoped<ITenantDiagnosticService, TenantDiagnosticService>();
        builder.Services.AddScoped<ISupportTicketService, SupportTicketService>();

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
        });

        var app = builder.Build();

        app.UseCors();
        app.UseDefaultFiles();
        app.UseStaticFiles();

        // Map Endpoints
        app.MapSupportEndpoints();
        app.MapFallbackToFile("index.html");

        // Seed default traces & tickets if needed
        using (var scope = app.Services.CreateScope())
        {
            var masterDb = scope.ServiceProvider.GetRequiredService<FlitflapMasterDbContext>();
            var tenantDb = scope.ServiceProvider.GetRequiredService<FlitflapTenantDbContext>();

            await masterDb.Database.EnsureCreatedAsync();
            await tenantDb.Database.EnsureCreatedAsync();

            // Seed sample delivery traces if empty
            if (!await tenantDb.DeliveryTraces.AnyAsync())
            {
                tenantDb.DeliveryTraces.AddRange(
                    new DeliveryTraceLog
                    {
                        MessageId = "msg-8472910-enterprise",
                        Sender = "partner@corporate.com",
                        Recipient = "info@flitflap.com",
                        EventStatus = "Delivered",
                        DiagnosticMessage = "250 2.0.0 OK: message queued for delivery into mailbox INBOX",
                        ClientIp = "192.0.2.45",
                        TimestampUtc = DateTime.UtcNow.AddMinutes(-12)
                    },
                    new DeliveryTraceLog
                    {
                        MessageId = "msg-5910234-billing",
                        Sender = "invoices@vendor.net",
                        Recipient = "demo@flitflap.com",
                        EventStatus = "Delivered",
                        DiagnosticMessage = "250 2.0.0 OK: SPF Pass, DKIM Verified, Spam Score 0.2",
                        ClientIp = "198.51.100.82",
                        TimestampUtc = DateTime.UtcNow.AddMinutes(-45)
                    },
                    new DeliveryTraceLog
                    {
                        MessageId = "msg-1029485-spam-trap",
                        Sender = "promotions@spam-domain-test.xyz",
                        Recipient = "info@flitflap.com",
                        EventStatus = "Filtered",
                        DiagnosticMessage = "Routed to Spam folder: Bayesian heuristic score 6.8 exceeds 5.0 threshold",
                        ClientIp = "203.0.113.19",
                        TimestampUtc = DateTime.UtcNow.AddHours(-2)
                    }
                );

                // Seed sample support tickets
                tenantDb.SupportTickets.AddRange(
                    new SupportTicket
                    {
                        TicketNumber = "TKT-1042",
                        DomainName = "flitflap.com",
                        ContactEmail = "admin@flitflap.com",
                        Subject = "Assist with DMARC p=quarantine alignment",
                        Category = "DNS / Deliverability",
                        Priority = "High",
                        Status = "Open",
                        Description = "Customer noticed outbound emails to Yahoo are intermittently flagged. Need review of DKIM selector and SPF include strings.",
                        CreatedAtUtc = DateTime.UtcNow.AddHours(-3)
                    },
                    new SupportTicket
                    {
                        TicketNumber = "TKT-1043",
                        DomainName = "flitflap.com",
                        ContactEmail = "it@flitflap.com",
                        Subject = "Mailbox info@flitflap.com approaching 90% quota ceiling",
                        Category = "Mailbox Quota",
                        Priority = "Medium",
                        Status = "Open",
                        Description = "Requested temporary headroom of +2 GB while archiver script runs.",
                        CreatedAtUtc = DateTime.UtcNow.AddHours(-1)
                    }
                );

                // Staff audit entry
                masterDb.StaffAuditLogs.Add(new StaffAuditLog
                {
                    StaffUser = "support@flitflap.com",
                    Action = "SupportDeskInitialized",
                    TargetTenantOrDomain = "flitflap.com",
                    Details = "Staff Support Console initialized with diagnostic telemetry, Mail Tracer, and ticketing queue."
                });

                await tenantDb.SaveChangesAsync();
                await masterDb.SaveChangesAsync();
            }
        }

        // Print Banner
        AnsiConsole.Write(new FigletText("Flitflap Support Desk").Color(Color.DeepSkyBlue1));
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[bold white]Property[/]");
        table.AddColumn("[bold cyan]Value[/]");
        table.AddRow("Application Scope", "Operator Scope: Staff Support Application (doc.md Section 2.A.2)");
        table.AddRow("Support Web Console", "[link=http://localhost:5003]http://localhost:5003[/]");
        table.AddRow("Database Provider", useSqlite ? "[yellow]Dual-Tier SQLite (Local Fallback)[/]" : "[green]PostgreSQL Master & Tenant Active[/]");
        table.AddRow("Mail Tracer Engine", "[green]Active (RFC 822 Hop Pipeline & Delivery Telemetry)[/]");
        table.AddRow("Assisted DNS Tool", "[bold yellow]Public DNS Verifier & Quick-Fix Generator Active[/]");
        AnsiConsole.Write(table);

        await app.RunAsync("http://0.0.0.0:5003");
    }
}

