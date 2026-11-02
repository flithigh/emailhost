using System.Text.RegularExpressions;
using EmailHost.ConsoleApp.Data;
using EmailHost.ConsoleApp.Entities;
using EmailHost.ConsoleApp.Security;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;

namespace EmailHost.ConsoleApp.Views;

public class AddDomainView
{
    private readonly FlitflapMasterDbContext _masterDb;
    private readonly FlitflapTenantDbContext _tenantDb;
    private readonly DkimService _dkimService;

    public AddDomainView(FlitflapMasterDbContext masterDb, FlitflapTenantDbContext tenantDb, DkimService dkimService)
    {
        _masterDb = masterDb;
        _tenantDb = tenantDb;
        _dkimService = dkimService;
    }

    public async Task RenderAsync()
    {
        ConsoleTheme.RenderHeader("Add Domain - DNS Provisioning Wizard", "Flitflap Email Host Domain Lifecycle (doc.md Section 2.B.2)");

        var tenants = await _masterDb.Tenants.ToListAsync();
        if (!tenants.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No tenant found. Auto-creating primary organization tenant...[/]");
            var newTenant = new Tenant { Name = "Primary Organization", SubscriptionTier = "Enterprise" };
            _masterDb.Tenants.Add(newTenant);
            await _masterDb.SaveChangesAsync();
            tenants.Add(newTenant);
        }

        var tenantChoices = tenants.Select(t => $"{t.Name} ({t.SubscriptionTier})").ToList();
        var selectedTenantName = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Select Tenant Organization:[/]")
                .AddChoices(tenantChoices));

        var tenant = tenants.First(t => $"{t.Name} ({t.SubscriptionTier})" == selectedTenantName);

        var domainName = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [bold yellow]Domain Name[/] (e.g. [grey]mycompany.com[/]):")
                .Validate(name =>
                {
                    name = name.Trim().ToLowerInvariant();
                    if (name.StartsWith("http://") || name.StartsWith("https://"))
                        return ValidationResult.Error("[red]Enter domain only without protocol (e.g. mycompany.com)[/]");

                    if (!Regex.IsMatch(name, @"^([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,}$"))
                        return ValidationResult.Error("[red]Invalid domain format. Example: mycompany.com[/]");

                    return ValidationResult.Success();
                }));

        domainName = domainName.Trim().ToLowerInvariant();

        if (await _masterDb.DomainsRegistry.AnyAsync(d => d.DomainName.ToLower() == domainName))
        {
            AnsiConsole.MarkupLine($"\n[red]Error:[/] Domain '[bold]{domainName}[/]' is already registered in Flitflap Master Registry!");
            ConsoleTheme.PressAnyKeyToContinue();
            return;
        }

        var verificationToken = $"flitflap-verify={Guid.NewGuid():N}";
        var domainReg = new DomainRegistry
        {
            TenantId = tenant.Id,
            DomainName = domainName,
            IsConnected = false,
            VerificationToken = verificationToken,
            DatabaseName = $"domain-db-{domainName.Replace(".", "-")}",
            CreatedAtUtc = DateTime.UtcNow
        };

        // Generate 2048-bit RSA DKIM Key
        var (privKey, pubKey, dkimDnsTxt) = _dkimService.GenerateDkimKeyPair(domainName, "default");
        var dkimEntity = new DkimKey
        {
            DomainName = domainName,
            Selector = "default",
            PrivateKeyPem = privKey,
            PublicKeyPem = pubKey,
            DnsTxtRecord = dkimDnsTxt,
            IsActive = true
        };

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Registering domain & generating 2048-bit DKIM cryptographic keys...", async _ =>
            {
                _masterDb.DomainsRegistry.Add(domainReg);
                _tenantDb.DkimKeys.Add(dkimEntity);
                await _masterDb.SaveChangesAsync();
                await _tenantDb.SaveChangesAsync();
            });

        AnsiConsole.MarkupLine($"\n[green]✔ Domain '[bold]{domainName}[/]' registered under Tenant '{tenant.Name}'![/]\n");

        // Display Full DNS Record Generator (doc.md Section 2.B.2)
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[bold cyan]Type[/]");
        table.AddColumn("[bold cyan]Host / Name[/]");
        table.AddColumn("[bold cyan]Target / Value[/]");
        table.AddColumn("[bold cyan]Priority / TTL[/]");

        table.AddRow(
            "[yellow]TXT (Verify)[/]",
            $"@",
            $"[white]{verificationToken}[/]",
            "3600"
        );
        table.AddRow(
            "[yellow]MX[/]",
            $"@",
            $"[white]mail.flitflap.com[/]",
            "10"
        );
        table.AddRow(
            "[yellow]TXT (SPF)[/]",
            $"@",
            $"[white]v=spf1 mx include:flitflap.com ~all[/]",
            "3600"
        );
        table.AddRow(
            "[yellow]TXT (DKIM)[/]",
            $"default._domainkey",
            $"[white]{dkimDnsTxt}[/]",
            "3600"
        );
        table.AddRow(
            "[yellow]TXT (DMARC)[/]",
            $"_dmarc",
            $"[white]v=DMARC1; p=quarantine; rua=mailto:dmarc-reports@flitflap.com; pct=100[/]",
            "3600"
        );
        table.AddRow(
            "[yellow]CNAME (Webmail)[/]",
            $"webmail",
            $"[white]mail.flitflap.com[/]",
            "3600"
        );

        var panel = new Panel(table)
        {
            Header = new PanelHeader($"[bold green] Required DNS Records for {domainName} [/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("green")
        };

        AnsiConsole.Write(panel);
        AnsiConsole.MarkupLine("\n[grey]Add these DNS records in your domain registrar (Cloudflare, GoDaddy, Route 53, etc.)[/]");

        ConsoleTheme.PressAnyKeyToContinue();
    }
}
