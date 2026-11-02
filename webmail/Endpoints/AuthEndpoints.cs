using System.Security.Claims;
using EmailHost.Webmail.Data;
using EmailHost.Webmail.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmailHost.Webmail.Endpoints;

public record LoginRequest(string Address, string Password);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/login", async (
            [FromBody] LoginRequest req,
            FlitflapTenantDbContext tenantDb,
            FlitflapMasterDbContext masterDb,
            IPasswordHasher hasher,
            JwtTokenService jwt) =>
        {
            if (string.IsNullOrWhiteSpace(req.Address) || string.IsNullOrWhiteSpace(req.Password))
            {
                return Results.BadRequest(new { message = "Email address and password are required." });
            }

            var address = req.Address.Trim().ToLowerInvariant();
            var mailbox = await tenantDb.Mailboxes.FirstOrDefaultAsync(m => m.Address.ToLower() == address);

            if (mailbox == null)
            {
                return Results.Unauthorized();
            }

            if (!mailbox.IsActive)
            {
                return Results.Problem("Mailbox account is disabled. Contact your administrator.", statusCode: 403);
            }

            if (!hasher.VerifyPassword(req.Password, mailbox.PasswordHash))
            {
                return Results.Unauthorized();
            }

            var token = jwt.GenerateToken(mailbox);

            return Results.Ok(new
            {
                token,
                user = new
                {
                    id = mailbox.Id,
                    address = mailbox.Address,
                    displayName = string.IsNullOrWhiteSpace(mailbox.DisplayName) ? mailbox.LocalPart : mailbox.DisplayName,
                    domain = mailbox.DomainName,
                    quotaBytes = mailbox.QuotaBytes,
                    usedBytes = mailbox.UsedBytes
                }
            });
        });

        group.MapGet("/me", [Authorize] async (
            ClaimsPrincipal user,
            FlitflapTenantDbContext tenantDb) =>
        {
            var mailboxIdStr = user.FindFirstValue("mailbox_id");
            if (!Guid.TryParse(mailboxIdStr, out var mailboxId)) return Results.Unauthorized();

            var mailbox = await tenantDb.Mailboxes.FindAsync(mailboxId);
            if (mailbox == null) return Results.NotFound();

            return Results.Ok(new
            {
                id = mailbox.Id,
                address = mailbox.Address,
                displayName = string.IsNullOrWhiteSpace(mailbox.DisplayName) ? mailbox.LocalPart : mailbox.DisplayName,
                domain = mailbox.DomainName,
                quotaBytes = mailbox.QuotaBytes,
                usedBytes = mailbox.UsedBytes,
                signatureHtml = mailbox.SignatureHtml,
                outOfOfficeEnabled = mailbox.OutOfOfficeEnabled
            });
        });

        group.MapPost("/change-password", [Authorize] async (
            ClaimsPrincipal user,
            [FromBody] ChangePasswordRequest req,
            FlitflapTenantDbContext tenantDb,
            IPasswordHasher hasher) =>
        {
            var mailboxIdStr = user.FindFirstValue("mailbox_id");
            if (!Guid.TryParse(mailboxIdStr, out var mailboxId)) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
            {
                return Results.BadRequest(new { message = "New password must be at least 6 characters." });
            }

            var mailbox = await tenantDb.Mailboxes.FindAsync(mailboxId);
            if (mailbox == null) return Results.NotFound();

            if (!hasher.VerifyPassword(req.CurrentPassword, mailbox.PasswordHash))
            {
                return Results.BadRequest(new { message = "Incorrect current password." });
            }

            mailbox.PasswordHash = hasher.HashPassword(req.NewPassword);
            mailbox.LastPasswordResetAtUtc = DateTime.UtcNow;
            await tenantDb.SaveChangesAsync();

            return Results.Ok(new { message = "Password updated successfully." });
        });
    }
}

