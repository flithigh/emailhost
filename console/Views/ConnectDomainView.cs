using DnsClient;
using EmailHost.ConsoleApp.Data;
using EmailHost.ConsoleApp.Entities;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;

namespace EmailHost.ConsoleApp.Views;

public class ConnectDomainView
{
    private readonly FlitflapMasterDbContext _masterDb;
    private readonly ILookupClient _dnsClient;

    public ConnectDomainView(FlitflapMasterDbContext masterDb)
    {
        _masterDb = masterDb;
        _dnsClient = new LookupClient(NameServer.GooglePublicDns, NameServer.Cloudflare);
    }

    public async Task RenderAsync()
    {
        ConsoleTheme.RenderHeader("Connect Domain - DNS Verification", "One-click DNS verification diagnostic (doc.md Section 2.B.2)");

        var domains = await _masterDb.DomainsRegistry.ToListAsync();
        if (domains.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No domains found. Please register a domain first using 'Add Domain'.[/]");
            ConsoleTheme.PressAnyKeyToContinue();
            return;
        }

        var domainChoices = domains
            .Select(d => $"{d.DomainName} [grey]({(d.IsConnected ? "[green]Connected[/]" : "[yellow]Pending[/]")})[/]")
            .ToList();
        domainChoices.Add("« Cancel");

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Select domain to verify and connect:[/]")
                .PageSize(10)
                .AddChoices(domainChoices));

        if (selection == "« Cancel") return;

        var domainName = selection.Split(' ')[0];
        var domain = domains.FirstOrDefault(d => d.DomainName == domainName);
        if (domain == null) return;

        ConsoleTheme.RenderHeader($"Verifying Domain: {domain.DomainName}");

        var method = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Verification Mode:[/]")
                .AddChoices(new[]
                {
                    "1. Live Public DNS Lookup (Check TXT token and MX record)",
                    "2. Manual / Development Connect (For local, staging, or private test environments)",
                    "« Cancel"
                }));

        if (method == "« Cancel") return;

        if (method.StartsWith("1."))
        {
            bool verified = false;
            string message = "";

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Aesthetic)
                .StartAsync($"Querying DNS servers for {domain.DomainName}...", async _ =>
                {
                    try
                    {
                        var txtQuery = await _dnsClient.QueryAsync(domain.DomainName, QueryType.TXT);
                        var foundTxts = txtQuery.Answers.TxtRecords().SelectMany(r => r.Text).ToList();

                        if (foundTxts.Any(t => t.Contains(domain.VerificationToken, StringComparison.OrdinalIgnoreCase)))
                        {
                            verified = true;
                            message = "TXT verification record matched on public DNS!";
                        }
                        else
                        {
                            message = $"Verification token '{domain.VerificationToken}' not found in public DNS.";
                        }
                    }
                    catch (Exception ex)
                    {
                        message = $"DNS resolution error: {ex.Message}";
                    }
                });

            if (verified)
            {
                domain.IsConnected = true;
                domain.VerifiedAtUtc = DateTime.UtcNow;
                await _masterDb.SaveChangesAsync();
                AnsiConsole.MarkupLine($"\n[bold green]✔ Domain '{domain.DomainName}' verified and connected successfully![/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"\n[bold red]✖ Verification failed:[/] [grey]{message}[/]\n");
                var force = AnsiConsole.Confirm("Would you like to force connect this domain manually for development / testing?", false);
                if (force)
                {
                    domain.IsConnected = true;
                    domain.VerifiedAtUtc = DateTime.UtcNow;
                    await _masterDb.SaveChangesAsync();
                    AnsiConsole.MarkupLine($"\n[green]✔ Domain '{domain.DomainName}' has been connected manually.[/]");
                }
            }
        }
        else if (method.StartsWith("2."))
        {
            domain.IsConnected = true;
            domain.VerifiedAtUtc = DateTime.UtcNow;
            await _masterDb.SaveChangesAsync();
            AnsiConsole.MarkupLine($"\n[bold green]✔ Domain '{domain.DomainName}' connected manually for development.[/]");
        }

        ConsoleTheme.PressAnyKeyToContinue();
    }
}
