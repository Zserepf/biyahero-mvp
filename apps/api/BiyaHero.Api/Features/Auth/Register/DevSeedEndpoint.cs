using BiyaHero.Api.Domain;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Features.Auth.Register;

/// <summary>
/// DEV-ONLY endpoint: POST /v1/dev/seed-role
/// Allows creating or promoting a user to any role in Development environment.
/// This endpoint is DISABLED in Production.
/// </summary>
public static class DevSeedEndpoint
{
    public static void MapDevSeedEndpoint(this IEndpointRouteBuilder app)
    {
        var env = app.ServiceProvider?.GetService<IWebHostEnvironment>()
                  ?? throw new InvalidOperationException("IWebHostEnvironment not available");

        // Only register in Development
        if (!env.IsDevelopment()) return;

        app.MapPost("/v1/dev/seed-role", async (
            DevSeedRequest request,
            IUserRepository userRepository,
            IPasswordHasher passwordHasher) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return Results.BadRequest(new { error = "Email and password required." });

            if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
                return Results.BadRequest(new { error = $"Invalid role. Valid: {string.Join(", ", Enum.GetNames<UserRole>())}" });

            // Check if user already exists — if so, just update their role
            var existing = await userRepository.FindByEmailAsync(request.Email);
            if (existing != null)
            {
                await userRepository.ChangeRoleAsync(existing.Id, role);
                existing.Status = UserStatus.Active;
                await userRepository.UpdateAsync(existing);
                return Results.Ok(new { message = $"Role updated to {role} for {request.Email}", userId = existing.Id });
            }

            // Create new user with the specified role
            var hash = passwordHasher.Hash(request.Password);
            var user = new User
            {
                Email = request.Email.Trim().ToLowerInvariant(),
                PasswordHash = hash,
                Role = role,
                Status = UserStatus.Active,
                DisplayName = request.DisplayName ?? $"{role} User"
            };

            await userRepository.CreateAsync(user);
            return Results.Ok(new { message = $"Created {role} account for {request.Email}", userId = user.Id });
        })
        .WithName("DevSeedRole")
        .WithTags("Dev")
        .AllowAnonymous();
    }
}

public sealed record DevSeedRequest(string Email, string Password, string Role, string? DisplayName);
