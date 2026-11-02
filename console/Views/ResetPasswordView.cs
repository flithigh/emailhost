using EmailHost.ConsoleApp.Data;
using EmailHost.ConsoleApp.Security;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;

namespace EmailHost.ConsoleApp.Views;

public class ResetPasswordView
{
    private readonly FlitflapTenantDbContext _tenantDb;
    private readonly IPasswordHasher _hasher;

    public ResetPasswordView(FlitflapTenantDbContext tenantDb, IPasswordHasher hasher)
    {
        _tenantDb = tenantDb;
        _hasher = hasher;
    }

    public async Task RenderAsync()
    {
        ConsoleTheme.RenderHeader("Reset Mailbox Password", "Update credentials with BCrypt encryption (doc.md Section 2.B.2)");

        var mailboxes = await _tenantDb.Mailboxes.ToListAsync();
        if (!mailboxes.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No mailboxes found. Please provision a mailbox first.[/]");
            ConsoleTheme.PressAnyKeyToContinue();
            return;
        }

        var mailboxChoices = mailboxes.Select(m => m.Address).ToList();
        mailboxChoices.Add("« Cancel");

        var selectedEmail = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Select mailbox account to reset password:[/]")
                .AddChoices(mailboxChoices));

        if (selectedEmail == "« Cancel") return;

        var mailbox = mailboxes.First(m => m.Address == selectedEmail);

        var newPassword = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [bold yellow]New Password[/]:")
                .PromptStyle("red")
                .Secret()
                .Validate(pw =>
                {
                    if (pw.Length < 8) return ValidationResult.Error("[red]Password must be at least 8 characters long.[/]");
                    return ValidationResult.Success();
                }));

        var confirmPassword = AnsiConsole.Prompt(
            new TextPrompt<string>("Confirm [bold yellow]New Password[/]:")
                .PromptStyle("red")
                .Secret());

        if (newPassword != confirmPassword)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Passwords do not match. Reset cancelled.");
            ConsoleTheme.PressAnyKeyToContinue();
            return;
        }

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Hashing and updating credentials in tenant shard...", async _ =>
            {
                mailbox.PasswordHash = _hasher.HashPassword(newPassword);
                mailbox.LastPasswordResetAtUtc = DateTime.UtcNow;
                _tenantDb.Mailboxes.Update(mailbox);
                await _tenantDb.SaveChangesAsync();
            });

        AnsiConsole.MarkupLine($"\n[bold green]✔ Password for '{mailbox.Address}' has been reset successfully![/]");
        ConsoleTheme.PressAnyKeyToContinue();
    }
}
