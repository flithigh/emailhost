using System.Text.RegularExpressions;
using EmailHost.ConsoleApp.Data;
using EmailHost.ConsoleApp.Entities;
using EmailHost.ConsoleApp.Security;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;

namespace EmailHost.ConsoleApp.Views;

public class AddMailboxView
{
    private readonly FlitflapMasterDbContext _masterDb;
    private readonly FlitflapTenantDbContext _tenantDb;
    private readonly IPasswordHasher _hasher;

    public AddMailboxView(FlitflapMasterDbContext masterDb, FlitflapTenantDbContext tenantDb, IPasswordHasher hasher)
    {
        _masterDb = masterDb;
        _tenantDb = tenantDb;
        _hasher = hasher;
    }

    public async Task RenderAsync()
    {
        ConsoleTheme.RenderHeader("Provision Mailbox / User Account", "Flitflap Mailbox Management (doc.md Section 2.B.2)");

        var domains = await _masterDb.DomainsRegistry.Where(d => d.IsActive).ToListAsync();
        if (!domains.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No active domains registered. Please add a domain first.[/]");
            ConsoleTheme.PressAnyKeyToContinue();
            return;
        }

        var domainChoices = domains.Select(d => d.DomainName).ToList();
        domainChoices.Add("« Cancel");

        var domainSelection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Select Domain for Mailbox:[/]")
                .AddChoices(domainChoices));

        if (domainSelection == "« Cancel") return;

        var username = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [bold]username / local-part[/] (e.g. [grey]admin[/], [grey]info[/], [grey]john[/]):")
                .Validate(name =>
                {
                    name = name.Trim().ToLowerInvariant();
                    if (!Regex.IsMatch(name, @"^[a-z0-9._%+-]+$"))
                        return ValidationResult.Error("[red]Invalid characters. Only alphanumeric, dots, and hyphens allowed.[/]");
                    return ValidationResult.Success();
                }));

        username = username.Trim().ToLowerInvariant();
        var fullEmail = $"{username}@{domainSelection}";

        if (await _tenantDb.Mailboxes.AnyAsync(m => m.Address.ToLower() == fullEmail.ToLower()))
        {
            AnsiConsole.MarkupLine($"\n[red]Error:[/] Mailbox '[bold]{fullEmail}[/]' already exists!");
            ConsoleTheme.PressAnyKeyToContinue();
            return;
        }

        var password = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter account password:")
                .PromptStyle("red")
                .Secret()
                .Validate(pw =>
                {
                    if (pw.Length < 8) return ValidationResult.Error("[red]Password must be at least 8 characters long.[/]");
                    return ValidationResult.Success();
                }));

        // Quota ceiling selection (doc.md Section 2.B.2: 5 GB, 20 GB)
        var quotaGb = AnsiConsole.Prompt(
            new SelectionPrompt<int>()
                .Title("[cyan]Select Mailbox Storage Quota Ceiling:[/]")
                .AddChoices(new[] { 2, 5, 10, 20, 50 }));

        var mailbox = new TenantMailbox
        {
            DomainName = domainSelection,
            LocalPart = username,
            Address = fullEmail,
            PasswordHash = _hasher.HashPassword(password),
            QuotaBytes = (long)quotaGb * 1024L * 1024L * 1024L,
            UsedBytes = 0,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Provisioning mailbox in tenant database shard...", async _ =>
            {
                _tenantDb.Mailboxes.Add(mailbox);
                await _tenantDb.SaveChangesAsync();
            });

        AnsiConsole.MarkupLine($"\n[bold green]✔ Mailbox '{fullEmail}' successfully provisioned![/]\n");

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Email Address");
        table.AddColumn("Domain");
        table.AddColumn("Quota Limit");
        table.AddColumn("Status");
        table.AddRow(fullEmail, domainSelection, $"{quotaGb} GB", "[green]Active[/]");

        AnsiConsole.Write(table);
        ConsoleTheme.PressAnyKeyToContinue();
    }
}
