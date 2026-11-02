using System.Security.Claims;
using EmailHost.Webmail.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmailHost.Webmail.Endpoints;

public record SettingsDto(
    string DisplayName,
    string SignatureHtml,
    bool OutOfOfficeEnabled,
    string OutOfOfficeSubject,
    string OutOfOfficeMessage
);

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, FlitflapTenantDbContext tenantDb) =>
        {
            var mailboxId = GetMailboxId(user);
            if (mailboxId == null) return Results.Unauthorized();

            var mailbox = await tenantDb.Mailboxes.FindAsync(mailboxId.Value);
            if (mailbox == null) return Results.NotFound();

            return Results.Ok(new SettingsDto(
                mailbox.DisplayName,
                mailbox.SignatureHtml,
                mailbox.OutOfOfficeEnabled,
                mailbox.OutOfOfficeSubject,
                mailbox.OutOfOfficeMessage
            ));
        });

        group.MapPut("/", async (
            [FromBody] SettingsDto dto,
            ClaimsPrincipal user,
            FlitflapTenantDbContext tenantDb) =>
        {
            var mailboxId = GetMailboxId(user);
            if (mailboxId == null) return Results.Unauthorized();

            var mailbox = await tenantDb.Mailboxes.FindAsync(mailboxId.Value);
            if (mailbox == null) return Results.NotFound();

            mailbox.DisplayName = dto.DisplayName ?? string.Empty;
            mailbox.SignatureHtml = dto.SignatureHtml ?? string.Empty;
            mailbox.OutOfOfficeEnabled = dto.OutOfOfficeEnabled;
            mailbox.OutOfOfficeSubject = dto.OutOfOfficeSubject ?? string.Empty;
            mailbox.OutOfOfficeMessage = dto.OutOfOfficeMessage ?? string.Empty;

            await tenantDb.SaveChangesAsync();
            return Results.Ok(dto);
        });
    }

    private static Guid? GetMailboxId(ClaimsPrincipal user)
    {
        var mailboxIdStr = user.FindFirstValue("mailbox_id");
        return Guid.TryParse(mailboxIdStr, out var id) ? id : null;
    }
}

