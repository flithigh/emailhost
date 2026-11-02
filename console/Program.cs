using EmailHost.ConsoleApp.Data;
using EmailHost.ConsoleApp.Entities;
using EmailHost.ConsoleApp.Security;
using EmailHost.ConsoleApp.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace EmailHost.ConsoleApp;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Build configuration
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var providerSetting = config["Database:Provider"] ?? "PostgreSQL";
        var masterPg = config["Database:MasterPostgreSql"] ?? "Host=localhost;Port=5432;Database=flitflap_master;Username=postgres;Password=postgres;";
        var tenantPg = config["Database:TenantPostgreSql"] ?? "Host=localhost;Port=5432;Database=flitflap_tenant_default;Username=postgres;Password=postgres;";
        var masterSqlite = config["Database:MasterSqlite"] ?? "Data Source=flitflap_master.db";
        var tenantSqlite = config["Database:TenantSqlite"] ?? "Data Source=flitflap_tenant.db";

        bool useSqlite = providerSetting.Equals("Sqlite", StringComparison.OrdinalIgnoreCase);

        // Pre-check PostgreSQL availability if PostgreSQL provider is selected
        if (!useSqlite)
        {
            var testOptions = new DbContextOptionsBuilder<FlitflapMasterDbContext>()
                .UseNpgsql(masterPg)
                .Options;

            using var testDb = new FlitflapMasterDbContext(testOptions);
            bool canConnect = false;
            try
            {
                canConnect = await testDb.Database.CanConnectAsync();
            }
            catch
            {
                canConnect = false;
            }

            if (!canConnect)
            {
                ConsoleTheme.RenderBanner();
                AnsiConsole.MarkupLine("[yellow]Notice: Cannot connect to PostgreSQL cluster on localhost:5432.[/]");
                AnsiConsole.MarkupLine($"[grey]Master DB Conn: {masterPg}[/]\n");

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[cyan]Select Database Operational Mode:[/]")
                        .AddChoices(new[]
                        {
                            "1. Use Local Dual-Tier SQLite (Instant local dev/test mode)",
                            "2. Continue with PostgreSQL (Ensure PostgreSQL service is active)",
                            "3. Exit to configure appsettings.json"
                        }));

                if (choice.StartsWith("1."))
                {
                    useSqlite = true;
                    AnsiConsole.MarkupLine("[green]✔ Switched to Local Dual-Tier SQLite mode.[/]");
                    Thread.Sleep(600);
                }
                else if (choice.StartsWith("3."))
                {
                    AnsiConsole.MarkupLine("[grey]Exiting. Configure 'appsettings.json' and restart.[/]");
                    return;
                }
            }
        }

        // Build DI Services
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);

        if (useSqlite)
        {
            services.AddDbContext<FlitflapMasterDbContext>(opt => opt.UseSqlite(masterSqlite));
            services.AddDbContext<FlitflapTenantDbContext>(opt => opt.UseSqlite(tenantSqlite));
        }
        else
        {
            services.AddDbContext<FlitflapMasterDbContext>(opt => opt.UseNpgsql(masterPg));
            services.AddDbContext<FlitflapTenantDbContext>(opt => opt.UseNpgsql(tenantPg));
        }

        // Security & DKIM
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<DkimService>();

        // Views
        services.AddTransient<AddDomainView>();
        services.AddTransient<ConnectDomainView>();
        services.AddTransient<ManageDomainView>();
        services.AddTransient<AddMailboxView>();
        services.AddTransient<ResetPasswordView>();
        services.AddTransient<RemoveMailView>();
        services.AddTransient<DatabaseStatusView>();

        var serviceProvider = services.BuildServiceProvider();

        // Initialize DB tables automatically & seed default tenant if needed
        using (var scope = serviceProvider.CreateScope())
        {
            var masterDb = scope.ServiceProvider.GetRequiredService<FlitflapMasterDbContext>();
            var tenantDb = scope.ServiceProvider.GetRequiredService<FlitflapTenantDbContext>();

            await masterDb.Database.EnsureCreatedAsync();
            await tenantDb.Database.EnsureCreatedAsync();

            if (!await masterDb.Tenants.AnyAsync())
            {
                masterDb.Tenants.Add(new Tenant
                {
                    Name = "Primary Organization",
                    SubscriptionTier = "Enterprise",
                    CreatedAtUtc = DateTime.UtcNow
                });
                await masterDb.SaveChangesAsync();
            }
        }

        // Run Main Application Loop
        await RunAppLoopAsync(serviceProvider, useSqlite);
    }

    private static async Task RunAppLoopAsync(IServiceProvider sp, bool usingSqlite)
    {
        while (true)
        {
            using var scope = sp.CreateScope();
            var masterDb = scope.ServiceProvider.GetRequiredService<FlitflapMasterDbContext>();
            var tenantDb = scope.ServiceProvider.GetRequiredService<FlitflapTenantDbContext>();

            int domainCount = 0;
            int connectedCount = 0;
            int mailboxesCount = 0;
            string tenantName = "Primary Organization";

            try
            {
                var tenant = await masterDb.Tenants.FirstOrDefaultAsync();
                if (tenant != null) tenantName = tenant.Name;

                var domains = await masterDb.DomainsRegistry.ToListAsync();
                domainCount = domains.Count;
                connectedCount = domains.Count(d => d.IsConnected);
                mailboxesCount = await tenantDb.Mailboxes.CountAsync();
            }
            catch
            {
                // In case of transient read error
            }

            ConsoleTheme.RenderBanner();

            // Status summary bar
            var statusGrid = new Grid();
            statusGrid.AddColumns(4);
            statusGrid.AddRow(
                $"[bold white]Active Tenant:[/] [yellow]{tenantName}[/]",
                $"[bold white]Domains:[/] [cyan]{domainCount}[/] ([green]{connectedCount} Connected[/])",
                $"[bold white]Mailboxes:[/] [cyan]{mailboxesCount}[/]",
                $"[bold white]Storage Mode:[/] [{(usingSqlite ? "yellow" : "green")}]{(usingSqlite ? "SQLite (Local)" : "PostgreSQL")}[/]"
            );

            var statusPanel = new Panel(statusGrid)
            {
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("deepskyblue1")
            };
            AnsiConsole.Write(statusPanel);
            AnsiConsole.WriteLine();

            var menu = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold deepskyblue1]Tenant Management Operations (doc.md Section 2.B.2):[/]")
                    .PageSize(12)
                    .AddChoices(new[]
                    {
                        "1. Add Domain (Register domain, 2048-bit RSA DKIM & DNS wizard)",
                        "2. Connect Domain (Verify public DNS & activate domain)",
                        "3. Manage Domains (Inspect domain registry, DNS records, tenant shards)",
                        "4. Provision Mailbox (Add email account with quota ceilings: 5 GB, 20 GB)",
                        "5. Reset Mailbox Password (Update credentials with BCrypt encryption)",
                        "6. Remove Mailbox (Permanently delete email account)",
                        "7. Storage & Database Diagnostics (PostgreSQL dual-tier & Garage S3 status)",
                        "0. Exit"
                    }));

            if (menu.StartsWith("0."))
            {
                AnsiConsole.MarkupLine("[bold cyan]Exiting Flitflap Email Host Client Console. Goodbye![/]");
                break;
            }

            try
            {
                if (menu.StartsWith("1."))
                {
                    var view = scope.ServiceProvider.GetRequiredService<AddDomainView>();
                    await view.RenderAsync();
                }
                else if (menu.StartsWith("2."))
                {
                    var view = scope.ServiceProvider.GetRequiredService<ConnectDomainView>();
                    await view.RenderAsync();
                }
                else if (menu.StartsWith("3."))
                {
                    var view = scope.ServiceProvider.GetRequiredService<ManageDomainView>();
                    await view.RenderAsync();
                }
                else if (menu.StartsWith("4."))
                {
                    var view = scope.ServiceProvider.GetRequiredService<AddMailboxView>();
                    await view.RenderAsync();
                }
                else if (menu.StartsWith("5."))
                {
                    var view = scope.ServiceProvider.GetRequiredService<ResetPasswordView>();
                    await view.RenderAsync();
                }
                else if (menu.StartsWith("6."))
                {
                    var view = scope.ServiceProvider.GetRequiredService<RemoveMailView>();
                    await view.RenderAsync();
                }
                else if (menu.StartsWith("7."))
                {
                    var view = scope.ServiceProvider.GetRequiredService<DatabaseStatusView>();
                    await view.RenderAsync();
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[bold red]Operation Error:[/] [white]{ex.Message}[/]");
                ConsoleTheme.PressAnyKeyToContinue();
            }
        }
    }
}
