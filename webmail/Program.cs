using System.Text;
using EmailHost.Webmail.Data;
using EmailHost.Webmail.Endpoints;
using EmailHost.Webmail.Entities;
using EmailHost.Webmail.Security;
using EmailHost.Webmail.Services;
using EmailHost.Webmail.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MimeKit;
using Spectre.Console;

namespace EmailHost.Webmail;

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

        // Security & Services
        builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        builder.Services.AddSingleton<JwtTokenService>();
        builder.Services.AddSingleton<IGarageStorageService, GarageStorageService>();
        builder.Services.AddScoped<IWebmailMailService, WebmailMailService>();

        // JWT Authentication
        var jwtSecret = config["Jwt:SecretKey"] ?? "flitflap_super_secret_jwt_key_that_is_at_least_32_bytes_long!";
        var jwtIssuer = config["Jwt:Issuer"] ?? "flitflap-webmail";
        var jwtAudience = config["Jwt:Audience"] ?? "flitflap-users";

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwtAudience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

        builder.Services.AddAuthorization();
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            });
        });

        var app = builder.Build();

        app.UseCors();
        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.UseAuthentication();
        app.UseAuthorization();

        // Map Endpoints
        app.MapAuthEndpoints();
        app.MapMailEndpoints();
        app.MapContactEndpoints();
        app.MapSettingsEndpoints();

        // SPA Fallback
        app.MapFallbackToFile("index.html");

        // Database auto-creation and demo mailbox seeding
        using (var scope = app.Services.CreateScope())
        {
            var masterDb = scope.ServiceProvider.GetRequiredService<FlitflapMasterDbContext>();
            var tenantDb = scope.ServiceProvider.GetRequiredService<FlitflapTenantDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var garage = scope.ServiceProvider.GetRequiredService<IGarageStorageService>();

            await masterDb.Database.EnsureCreatedAsync();
            await tenantDb.Database.EnsureCreatedAsync();

            // Ensure tenant
            var tenant = await masterDb.Tenants.FirstOrDefaultAsync();
            if (tenant == null)
            {
                tenant = new Tenant
                {
                    Name = "Flitflap Enterprise",
                    SubscriptionTier = "Enterprise",
                    CreatedAtUtc = DateTime.UtcNow
                };
                masterDb.Tenants.Add(tenant);
                await masterDb.SaveChangesAsync();
            }

            // Ensure domain
            var domain = await masterDb.DomainsRegistry.FirstOrDefaultAsync(d => d.DomainName == "flitflap.com");
            if (domain == null)
            {
                domain = new DomainRegistry
                {
                    TenantId = tenant.Id,
                    DomainName = "flitflap.com",
                    DatabaseName = "flitflap_tenant_default",
                    IsConnected = true,
                    IsActive = true,
                    VerificationToken = "flitflap-token-enterprise"
                };
                masterDb.DomainsRegistry.Add(domain);
                await masterDb.SaveChangesAsync();
            }

            // Ensure default mailboxes (info@flitflap.com & demo@flitflap.com)
            var infoBox = await tenantDb.Mailboxes.FirstOrDefaultAsync(m => m.Address == "info@flitflap.com");
            if (infoBox == null)
            {
                var passwordHash = hasher.HashPassword("Password123!");
                infoBox = new TenantMailbox
                {
                    Address = "info@flitflap.com",
                    DisplayName = "Flitflap Info Desk",
                    LocalPart = "info",
                    DomainName = "flitflap.com",
                    PasswordHash = passwordHash,
                    QuotaBytes = 5L * 1024L * 1024L * 1024L,
                    UsedBytes = 2450000,
                    SignatureHtml = "<p style='font-family:sans-serif;color:#38bdf8'><b>Flitflap Info Desk</b><br>Email: info@flitflap.com<br>Web: https://flitflap.com</p>"
                };
                tenantDb.Mailboxes.Add(infoBox);

                var demoBox = new TenantMailbox
                {
                    Address = "demo@flitflap.com",
                    DisplayName = "Demo Account",
                    LocalPart = "demo",
                    DomainName = "flitflap.com",
                    PasswordHash = passwordHash,
                    QuotaBytes = 5L * 1024L * 1024L * 1024L,
                    UsedBytes = 1200000
                };
                tenantDb.Mailboxes.Add(demoBox);
                await tenantDb.SaveChangesAsync();

                // Seed initial Welcome Emails in INBOX
                var welcomeMime = new MimeMessage();
                welcomeMime.From.Add(new MailboxAddress("Flitflap System", "system@flitflap.com"));
                welcomeMime.To.Add(new MailboxAddress(infoBox.DisplayName, infoBox.Address));
                welcomeMime.Subject = "Welcome to Flitflap Email Host!";

                var bb = new BodyBuilder();
                bb.HtmlBody = @"<div style='font-family: -apple-system, BlinkMacSystemFont, Segoe UI, Roboto, sans-serif; max-width: 600px; padding: 20px; color: #334155; line-height: 1.6;'>
                    <h2 style='color: #0284c7; margin-bottom: 8px;'>Welcome to your new Flitflap Mailbox!</h2>
                    <p>Your high-performance, enterprise email hosting mailbox is now fully provisioned.</p>
                    <div style='background: #f8fafc; border-left: 4px solid #0284c7; padding: 12px 16px; margin: 16px 0; border-radius: 4px;'>
                        <b>Key Specs:</b>
                        <ul style='margin: 8px 0 0 18px; padding: 0;'>
                            <li>Dual-tier PostgreSQL metadata isolation</li>
                            <li>Garage S3 object storage for raw MIME and attachments</li>
                            <li>2048-bit RSA DKIM signing & SPF/DMARC policy enforcement</li>
                            <li>Real-time anti-spam and anti-virus protection</li>
                        </ul>
                    </div>
                    <p>Feel free to compose messages, organize contacts, and test the email suite.</p>
                    <p style='color: #64748b; font-size: 0.9em;'>The Flitflap Engineering Team</p>
                </div>";
                welcomeMime.Body = bb.ToMessageBody();

                var welcomeKey = await garage.SaveMimeMessageAsync(tenant.Id.ToString(), "flitflap.com", infoBox.Id.ToString(), welcomeMime);

                tenantDb.MessagesIndex.Add(new MessageIndex
                {
                    MailboxId = infoBox.Id,
                    Folder = "INBOX",
                    Sender = "system@flitflap.com",
                    Recipient = infoBox.Address,
                    Subject = "Welcome to Flitflap Email Host!",
                    PreviewSnippet = "Welcome to your new Flitflap Mailbox! Your high-performance, enterprise email hosting mailbox is now fully provisioned.",
                    SizeBytes = 24500,
                    IsRead = false,
                    IsFlagged = true,
                    SpfVerdict = "Pass",
                    DkimVerdict = "Pass",
                    DmarcVerdict = "Pass",
                    SpamScore = 0.0,
                    GarageStorageKey = welcomeKey,
                    CreatedAtUtc = DateTime.UtcNow,
                    ReceivedAtUtc = DateTime.UtcNow
                });

                // Seed a demo contact
                tenantDb.Contacts.Add(new Contact
                {
                    MailboxId = infoBox.Id,
                    Name = "Dev Operations",
                    Email = "devops@flitflap.com",
                    Company = "Flitflap Cloud",
                    Notes = "Platform infrastructure team"
                });

                await tenantDb.SaveChangesAsync();
            }
        }

        // Print Startup Banner
        AnsiConsole.Write(new FigletText("Flitflap Webmail").Color(Color.DeepSkyBlue1));
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[bold white]Property[/]");
        table.AddColumn("[bold cyan]Value[/]");
        table.AddRow("Application Scope", "Consumer Tier: Webmail Client Application (doc.md Section 2.B.3)");
        table.AddRow("Webmail UI URL", "[link=http://localhost:5002]http://localhost:5002[/]");
        table.AddRow("Database Provider", useSqlite ? "[yellow]Dual-Tier SQLite (Local Fallback)[/]" : "[green]PostgreSQL Dual-Tier Active[/]");
        table.AddRow("Garage S3 Storage", "[green]Active (flitflap-mail-store)[/]");
        table.AddRow("Default User", "[bold yellow]info@flitflap.com[/] (Password: [grey]Password123![/])");
        AnsiConsole.Write(table);

        await app.RunAsync("http://0.0.0.0:5002");
    }
}

