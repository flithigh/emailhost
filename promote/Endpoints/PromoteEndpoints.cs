using EmailHost.Promote.Services;
using Microsoft.AspNetCore.Mvc;

namespace EmailHost.Promote.Endpoints;

public static class PromoteEndpoints
{
    public static void MapPromoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/promote");

        // 1. Deliverability & MX Test Tool
        group.MapGet("/deliverability", async ([FromQuery] string domain, IDeliverabilityTestService testService) =>
        {
            if (string.IsNullOrWhiteSpace(domain))
                return Results.BadRequest(new { message = "Domain name is required." });

            var result = await testService.TestDomainDeliverabilityAsync(domain);
            return Results.Ok(result);
        });

        // 2. Self-Service Signup Gateway
        group.MapPost("/signup", async ([FromBody] SignupRequest request, ITenantOnboardingService onboardingService) =>
        {
            if (string.IsNullOrWhiteSpace(request.OrganizationName) ||
                string.IsNullOrWhiteSpace(request.DomainName) ||
                string.IsNullOrWhiteSpace(request.AdminEmail) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.BadRequest(new { message = "All fields (Organization, Domain, Email, Password) are required." });
            }

            if (request.Password.Length < 6)
            {
                return Results.BadRequest(new { message = "Password must be at least 6 characters long." });
            }

            try
            {
                var response = await onboardingService.RegisterTenantAsync(request);
                return Results.Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        // 3. Dynamic Pricing Model
        group.MapGet("/pricing", (IConfiguration config) =>
        {
            return Results.Ok(new
            {
                starter = new { basePriceUsd = 19, storageLimitGb = 50, includedDomains = 1, includedMailboxes = 10 },
                pro = new { basePriceUsd = 49, storageLimitGb = 250, includedDomains = 5, includedMailboxes = 50 },
                enterprise = new { basePriceUsd = 149, storageLimitGb = 1000, includedDomains = 20, includedMailboxes = 250 },
                pricePerAdditionalMailboxUsd = 1.50,
                pricePerAdditionalDomainUsd = 5.00
            });
        });
    }
}

