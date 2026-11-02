using EmailHost.ConsoleApp.Data;
using EmailHost.ConsoleApp.Entities;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;

namespace EmailHost.ConsoleApp.Views;

public class ManageDomainView
{
    private readonly FlitflapMasterDbContext _masterDb;
    private readonly FlitflapTenantDbContext _tenantDb;

    public ManageDomainView(FlitflapMasterDbContext masterDb, FlitflapTenantDbContext tenantDb)
    {
        _masterDb = masterDb;
        _tenantDb = tenantDb;
    }

    public async Task RenderAsync()
    {
        while (true)
        {
            ConsoleTheme.RenderHeader("Manage Domains", "Flitflap Master Domain Registry & Tenant Shards");

            var domains = await _masterDb.DomainsRegistry.Include(d => d.Tenant).ToListAsync();

            if (domains.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No domains registered yet.[/]\n");
                AnsiConsole.MarkupLine("[grey]Use 'Add Domain' to register your first domain.[/]");
                ConsoleTheme.PressAnyKeyToContinue();
                return;
            }

            var table = new Table().Border(TableBorder.Rounded).Expand();
            table.AddColumn("[bold]#[/]");
            table.AddColumn("[bold]Domain Name[/]");
            table.AddColumn("[bold]Tenant Organization[/]");
            table.AddColumn("[bold]Status[/]");
            table.AddColumn("[bold]Tenant Database Shard[/]");
            table.AddColumn("[bold]Created (UTC)[/]");

            for (int i = 0; i < domains.Count; i++)
            {
                var d = domains[i];
                var statusMarkup = d.IsConnected ? "[green]● Connected[/]" : "[yellow]○ Pending Verification[/]";

                table.AddRow(
                    (i + 1).ToString(),
                    $"[bold white]{Markup.Escape(d.DomainName)}[/]",
                    $"[cyan]{Markup.Escape(d.Tenant?.Name ?? "Primary")}[/]",
                    statusMarkup,
                    $"[grey]{Markup.Escape(d.DatabaseName)}[/]",
                    d.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm")
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan]Select domain action:[/]")
                    .PageSize(10)
                    .AddChoices(new[]
                    {
                        "View Domain Details & Security Keys",
                        "Delete Domain",
                        "« Back to Main Menu"
                    }));

            if (action == "« Back to Main Menu") return;

            if (action.StartsWith("View"))
            {
                await ShowDomainDetailsAsync(domains);
            }
            else if (action.StartsWith("Delete"))
            {
                await DeleteDomainPromptAsync(domains);
            }
        }
    }

    private async Task ShowDomainDetailsAsync(List<DomainRegistry> domains)
    {
        var domain = SelectDomain(domains, "Select a domain to inspect:");
        if (domain == null) return;

        ConsoleTheme.RenderHeader($"Domain Details: {domain.DomainName}");

        var dkimKey = await _tenantDb.DkimKeys.FirstOrDefaultAsync(k => k.DomainName.ToLower() == domain.DomainName.ToLower());
        var mailboxesCount = await _tenantDb.Mailboxes.CountAsync(m => m.DomainName.ToLower() == domain.DomainName.ToLower());

        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow("[bold grey]Domain Name:[bind]", $"[bold white]{Markup.Escape(domain.DomainName)}[/]");
        grid.AddRow("[bold grey]Tenant Organization:[bind]", $"[cyan]{Markup.Escape(domain.Tenant?.Name ?? "N/A")}[/]");
        grid.AddRow("[bold grey]Connection Status:[bind]", domain.IsConnected ? "[bold green]Connected[/]" : "[bold yellow]Pending Verification[/]");
        grid.AddRow("[bold grey]Verification Token:[bind]", $"[cyan]{Markup.Escape(domain.VerificationToken)}[/]");
        grid.AddRow("[bold grey]Database Shard:[bind]", $"[white]{Markup.Escape(domain.DatabaseName)}[/]");
        grid.AddRow("[bold grey]DKIM Selector:[bind]", $"[yellow]{dkimKey?.Selector ?? "None"}[/]");
        grid.AddRow("[bold grey]Active Mailboxes:[bind]", $"[cyan]{mailboxesCount}[/]");
        grid.AddRow("[bold grey]Created Date:[bind]", domain.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss UTC"));

        var panel = new Panel(grid)
        {
            Header = new PanelHeader($"[bold cyan] {domain.DomainName} [/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(panel);

        if (dkimKey != null && !string.IsNullOrEmpty(dkimKey.DnsTxtRecord))
        {
            AnsiConsole.MarkupLine("\n[bold]DKIM DNS TXT Record Value:[/]");
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(dkimKey.DnsTxtRecord)}[/]");
        }

        ConsoleTheme.PressAnyKeyToContinue();
    }

    private async Task DeleteDomainPromptAsync(List<DomainRegistry> domains)
    {
        var domain = SelectDomain(domains, "Select a domain to delete:");
        if (domain == null) return;

        var confirm = AnsiConsole.Confirm(
            $"[red]Are you sure you want to permanently delete domain '[bold]{domain.DomainName}[/]' from the registry?[/]",
            false);

        if (!confirm) return;

        _masterDb.DomainsRegistry.Remove(domain);
        await _masterDb.SaveChangesAsync();

        AnsiConsole.MarkupLine($"[green]✔ Domain '{domain.DomainName}' removed from master registry.[/]");
        ConsoleTheme.PressAnyKeyToContinue();
    }

    private DomainRegistry? SelectDomain(List<DomainRegistry> domains, string prompt)
    {
        var choices = domains.Select(d => $"{d.DomainName} ({(d.IsConnected ? "Connected" : "Pending")})").ToList();
        choices.Add("« Cancel");

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[cyan]{Markup.Escape(prompt)}[/]")
                .AddChoices(choices));

        if (selection == "« Cancel") return null;

        var selectedDomainName = selection.Split(' ')[0];
        return domains.FirstOrDefault(d => d.DomainName == selectedDomainName);
    }
}
