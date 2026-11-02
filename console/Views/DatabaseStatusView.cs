using Amazon.S3;
using EmailHost.ConsoleApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

namespace EmailHost.ConsoleApp.Views;

public class DatabaseStatusView
{
    private readonly FlitflapMasterDbContext _masterDb;
    private readonly FlitflapTenantDbContext _tenantDb;
    private readonly IConfiguration _config;

    public DatabaseStatusView(FlitflapMasterDbContext masterDb, FlitflapTenantDbContext tenantDb, IConfiguration config)
    {
        _masterDb = masterDb;
        _tenantDb = tenantDb;
        _config = config;
    }

    public async Task RenderAsync()
    {
        ConsoleTheme.RenderHeader("Storage & Database Diagnostics", "PostgreSQL Dual-Tier & Garage S3 Storage Status (doc.md Section 3)");

        bool masterOnline = false;
        bool tenantOnline = false;
        bool garageOnline = false;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Running diagnostics across PostgreSQL and Garage S3...", async _ =>
            {
                try { masterOnline = await _masterDb.Database.CanConnectAsync(); } catch { }
                try { tenantOnline = await _tenantDb.Database.CanConnectAsync(); } catch { }

                // Check Garage S3
                try
                {
                    var endpoint = _config["Garage:Endpoint"] ?? "http://localhost:3900";
                    var accessKey = _config["Garage:AccessKey"] ?? "garage_access_key";
                    var secretKey = _config["Garage:SecretKey"] ?? "garage_secret_key";
                    var s3Config = new AmazonS3Config
                    {
                        ServiceURL = endpoint,
                        ForcePathStyle = true,
                        AuthenticationRegion = "garage",
                        Timeout = TimeSpan.FromSeconds(2)
                    };
                    using var client = new AmazonS3Client(accessKey, secretKey, s3Config);
                    await client.ListBucketsAsync();
                    garageOnline = true;
                }
                catch
                {
                    garageOnline = false;
                }
            });

        int tenantsCount = masterOnline ? await _masterDb.Tenants.CountAsync() : 0;
        int domainsCount = masterOnline ? await _masterDb.DomainsRegistry.CountAsync() : 0;
        int mailboxesCount = tenantOnline ? await _tenantDb.Mailboxes.CountAsync() : 0;
        int dkimKeysCount = tenantOnline ? await _tenantDb.DkimKeys.CountAsync() : 0;
        int messagesCount = tenantOnline ? await _tenantDb.MessagesIndex.CountAsync() : 0;

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Component");
        table.AddColumn("Target / Scope");
        table.AddColumn("Health Status");
        table.AddColumn("Metrics");

        table.AddRow(
            "[bold white]Master Database[/]",
            "Global Tier (`flitflap_master`)",
            masterOnline ? "[bold green]Online[/]" : "[bold red]Offline / Using SQLite[/]",
            $"{tenantsCount} Tenants, {domainsCount} Domains"
        );

        table.AddRow(
            "[bold white]Tenant Shard Database[/]",
            "Tenant Tier (`domain-database-XXX`)",
            tenantOnline ? "[bold green]Online[/]" : "[bold red]Offline / Using SQLite[/]",
            $"{mailboxesCount} Mailboxes, {dkimKeysCount} DKIM Keys"
        );

        table.AddRow(
            "[bold white]Garage S3 Object Store[/]",
            "Bucket: `flitflap-mail-store`",
            garageOnline ? "[bold green]Online (S3 API)[/]" : "[bold yellow]Offline (Using Local Fallback)[/]",
            $"{messagesCount} Messages Indexed"
        );

        var panel = new Panel(table)
        {
            Header = new PanelHeader("[bold cyan] Flitflap Storage Matrix [/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(panel);

        AnsiConsole.MarkupLine("\n[grey]doc.md Section 3: PostgreSQL handles relational metadata, searching, and flags;[/]");
        AnsiConsole.MarkupLine("[grey]Garage S3 stores full RFC 822 MIME raw emails (.eml) and attachments.[/]");

        ConsoleTheme.PressAnyKeyToContinue();
    }
}
