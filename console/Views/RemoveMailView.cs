using EmailHost.ConsoleApp.Data;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;

namespace EmailHost.ConsoleApp.Views;

public class RemoveMailView
{
    private readonly FlitflapTenantDbContext _tenantDb;

    public RemoveMailView(FlitflapTenantDbContext tenantDb)
    {
        _tenantDb = tenantDb;
    }

    public async Task RenderAsync()
    {
        ConsoleTheme.RenderHeader("Remove Mailbox", "Permanently delete a mailbox account (doc.md Section 2.B.2)");

        var mailboxes = await _tenantDb.Mailboxes.ToListAsync();
        if (!mailboxes.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No mailboxes found.[/]");
            ConsoleTheme.PressAnyKeyToContinue();
            return;
        }

        var mailboxChoices = mailboxes.Select(m => m.Address).ToList();
        mailboxChoices.Add("« Cancel");

        var selectedEmail = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[red]Select mailbox to permanently delete:[/]")
                .AddChoices(mailboxChoices));

        if (selectedEmail == "« Cancel") return;

        var mailbox = mailboxes.First(m => m.Address == selectedEmail);

        var confirm = AnsiConsole.Confirm(
            $"[red]Are you sure you want to permanently delete mailbox '[bold]{mailbox.Address}[/]' and its indexed messages?[/]",
            false);

        if (!confirm)
        {
            AnsiConsole.MarkupLine("[yellow]Deletion cancelled.[/]");
            ConsoleTheme.PressAnyKeyToContinue();
            return;
        }

        _tenantDb.Mailboxes.Remove(mailbox);
        await _tenantDb.SaveChangesAsync();

        AnsiConsole.MarkupLine($"\n[bold green]✔ Mailbox '{mailbox.Address}' successfully removed.[/]");
        ConsoleTheme.PressAnyKeyToContinue();
    }
}
