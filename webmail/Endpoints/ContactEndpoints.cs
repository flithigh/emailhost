using System.Security.Claims;
using EmailHost.Webmail.Data;
using EmailHost.Webmail.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmailHost.Webmail.Endpoints;

public record ContactDto(Guid? Id, string Name, string Email, string? Company, string? Phone, string? Notes);

public static class ContactEndpoints
{
    public static void MapContactEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/contacts").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, FlitflapTenantDbContext tenantDb) =>
        {
            var mailboxId = GetMailboxId(user);
            if (mailboxId == null) return Results.Unauthorized();

            var contacts = await tenantDb.Contacts
                .Where(c => c.MailboxId == mailboxId.Value)
                .OrderBy(c => c.Name)
                .Select(c => new ContactDto(c.Id, c.Name, c.Email, c.Company, c.Phone, c.Notes))
                .ToListAsync();

            return Results.Ok(contacts);
        });

        group.MapPost("/", async (
            [FromBody] ContactDto dto,
            ClaimsPrincipal user,
            FlitflapTenantDbContext tenantDb) =>
        {
            var mailboxId = GetMailboxId(user);
            if (mailboxId == null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.Email))
                return Results.BadRequest(new { message = "Contact email is required." });

            var contact = new Contact
            {
                MailboxId = mailboxId.Value,
                Name = string.IsNullOrWhiteSpace(dto.Name) ? dto.Email : dto.Name,
                Email = dto.Email.Trim().ToLowerInvariant(),
                Company = dto.Company ?? string.Empty,
                Phone = dto.Phone ?? string.Empty,
                Notes = dto.Notes ?? string.Empty
            };

            tenantDb.Contacts.Add(contact);
            await tenantDb.SaveChangesAsync();

            return Results.Created($"/api/contacts/{contact.Id}", new ContactDto(contact.Id, contact.Name, contact.Email, contact.Company, contact.Phone, contact.Notes));
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] ContactDto dto,
            ClaimsPrincipal user,
            FlitflapTenantDbContext tenantDb) =>
        {
            var mailboxId = GetMailboxId(user);
            if (mailboxId == null) return Results.Unauthorized();

            var contact = await tenantDb.Contacts.FirstOrDefaultAsync(c => c.Id == id && c.MailboxId == mailboxId.Value);
            if (contact == null) return Results.NotFound();

            contact.Name = dto.Name;
            contact.Email = dto.Email.Trim().ToLowerInvariant();
            contact.Company = dto.Company ?? string.Empty;
            contact.Phone = dto.Phone ?? string.Empty;
            contact.Notes = dto.Notes ?? string.Empty;

            await tenantDb.SaveChangesAsync();
            return Results.Ok(new ContactDto(contact.Id, contact.Name, contact.Email, contact.Company, contact.Phone, contact.Notes));
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            FlitflapTenantDbContext tenantDb) =>
        {
            var mailboxId = GetMailboxId(user);
            if (mailboxId == null) return Results.Unauthorized();

            var contact = await tenantDb.Contacts.FirstOrDefaultAsync(c => c.Id == id && c.MailboxId == mailboxId.Value);
            if (contact == null) return Results.NotFound();

            tenantDb.Contacts.Remove(contact);
            await tenantDb.SaveChangesAsync();
            return Results.Ok();
        });
    }

    private static Guid? GetMailboxId(ClaimsPrincipal user)
    {
        var mailboxIdStr = user.FindFirstValue("mailbox_id");
        return Guid.TryParse(mailboxIdStr, out var id) ? id : null;
    }
}

