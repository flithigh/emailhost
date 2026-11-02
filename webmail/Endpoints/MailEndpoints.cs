using System.Security.Claims;
using EmailHost.Webmail.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmailHost.Webmail.Endpoints;

public record FlagRequest(bool? IsFlagged);
public record ReadRequest(bool? IsRead);
public record MoveRequest(string TargetFolder);

public static class MailEndpoints
{
    public static void MapMailEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/mail").RequireAuthorization();

        group.MapGet("/folders", async (ClaimsPrincipal user, IWebmailMailService mailService) =>
        {
            var mailboxId = GetMailboxId(user);
            if (mailboxId == null) return Results.Unauthorized();

            var folders = await mailService.GetFolderStatsAsync(mailboxId.Value);
            return Results.Ok(folders);
        });

        group.MapGet("/messages", async (
            ClaimsPrincipal user,
            IWebmailMailService mailService,
            [FromQuery] string folder = "INBOX",
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20) =>
        {
            var mailboxId = GetMailboxId(user);
            if (mailboxId == null) return Results.Unauthorized();

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 100);

            var (items, total) = await mailService.GetMessagesPagedAsync(mailboxId.Value, folder, search, page, pageSize);
            return Results.Ok(new
            {
                items,
                total,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)total / pageSize)
            });
        });

        group.MapGet("/messages/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            IWebmailMailService mailService) =>
        {
            var mailboxId = GetMailboxId(user);
            if (mailboxId == null) return Results.Unauthorized();

            var detail = await mailService.GetMessageDetailAsync(mailboxId.Value, id);
            if (detail == null) return Results.NotFound();

            return Results.Ok(detail);
        });

        group.MapGet("/messages/{id:guid}/eml", async (
            Guid id,
            ClaimsPrincipal user,
            IWebmailMailService mailService) =>
        {
            var mailboxId = GetMailboxId(user);
            if (mailboxId == null) return Results.Unauthorized();

            var (stream, filename) = await mailService.GetRawEmlStreamAsync(mailboxId.Value, id);
            if (stream == null) return Results.NotFound();

            return Results.File(stream, "message/rfc822", filename);
        });

        group.MapGet("/messages/{id:guid}/attachments/{filename}", async (
            Guid id,
            string filename,
            ClaimsPrincipal user,
            IWebmailMailService mailService) =>
        {
            var mailboxId = GetMailboxId(user);
            if (mailboxId == null) return Results.Unauthorized();

            var res = await mailService.GetAttachmentStreamAsync(mailboxId.Value, id, filename);
            if (res == null || res.Value.Stream == null) return Results.NotFound();

            return Results.File(res.Value.Stream, res.Value.ContentType, res.Value.FileName);
        });

        group.MapPost("/send", async (
            [FromBody] SendEmailRequest request,
            ClaimsPrincipal user,
            IWebmailMailService mailService) =>
        {
            var mailboxId = GetMailboxId(user);
            if (mailboxId == null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.To))
            {
                return Results.BadRequest(new { message = "Recipient (To) is required." });
            }

            try
            {
                var messageId = await mailService.SendEmailAsync(mailboxId.Value, request);
                return Results.Ok(new { messageId, message = "Email dispatched successfully." });
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        group.MapPost("/draft", async (
            [FromBody] SendEmailRequest request,
            ClaimsPrincipal user,
            IWebmailMailService mailService) =>
        {
            var mailboxId = GetMailboxId(user);
            if (mailboxId == null) return Results.Unauthorized();

            var messageId = await mailService.SaveDraftAsync(mailboxId.Value, request);
            return Results.Ok(new { messageId, message = "Draft saved." });
        });

        group.MapPost("/messages/{id:guid}/flag", async (
            Guid id,
            [FromBody] FlagRequest req,
            ClaimsPrincipal user,
            IWebmailMailService mailService) =>
        {
            var mailboxId = GetMailboxId(user);
            if (mailboxId == null) return Results.Unauthorized();

            var success = await mailService.ToggleFlagAsync(mailboxId.Value, id, req?.IsFlagged);
            return success ? Results.Ok() : Results.NotFound();
        });

        group.MapPost("/messages/{id:guid}/read", async (
            Guid id,
            [FromBody] ReadRequest req,
            ClaimsPrincipal user,
            IWebmailMailService mailService) =>
        {
            var mailboxId = GetMailboxId(user);
            if (mailboxId == null) return Results.Unauthorized();

            var success = await mailService.ToggleReadAsync(mailboxId.Value, id, req?.IsRead);
            return success ? Results.Ok() : Results.NotFound();
        });

        group.MapPost("/messages/{id:guid}/move", async (
            Guid id,
            [FromBody] MoveRequest req,
            ClaimsPrincipal user,
            IWebmailMailService mailService) =>
        {
            var mailboxId = GetMailboxId(user);
            if (mailboxId == null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(req.TargetFolder))
                return Results.BadRequest(new { message = "Target folder required." });

            var success = await mailService.MoveMessageAsync(mailboxId.Value, id, req.TargetFolder);
            return success ? Results.Ok() : Results.NotFound();
        });

        group.MapDelete("/messages/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            IWebmailMailService mailService) =>
        {
            var mailboxId = GetMailboxId(user);
            if (mailboxId == null) return Results.Unauthorized();

            var success = await mailService.DeleteMessageAsync(mailboxId.Value, id);
            return success ? Results.Ok() : Results.NotFound();
        });
    }

    private static Guid? GetMailboxId(ClaimsPrincipal user)
    {
        var mailboxIdStr = user.FindFirstValue("mailbox_id");
        return Guid.TryParse(mailboxIdStr, out var id) ? id : null;
    }
}

