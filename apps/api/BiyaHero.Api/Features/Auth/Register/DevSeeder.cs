using BiyaHero.Api.Domain;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Features.Auth.Register;

/// <summary>
/// Startup seeder — runs automatically on every Development launch.
/// Reads seed accounts from the "DevSeed:Accounts" config section and
/// upserts them into the user store (creates if missing, skips if already present).
///
/// Safe to run on every restart: it checks email existence before inserting.
/// Disabled in Production.
///
/// Configure accounts in appsettings.Development.json:
/// {
///   "DevSeed": {
///     "Accounts": [
///       { "Email": "...", "Password": "...", "Role": "SuperAdmin", "DisplayName": "..." },
///       { "Email": "...", "Password": "...", "Role": "Moderator",  "DisplayName": "..." }
///     ]
///   }
/// }
/// </summary>
public static class DevSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IWebHostEnvironment env)
    {
        // Only run in Development
        if (!env.IsDevelopment()) return;

        var config      = services.GetRequiredService<IConfiguration>();
        var userRepo    = services.GetRequiredService<IUserRepository>();
        var hasher      = services.GetRequiredService<IPasswordHasher>();
        var logger      = services.GetRequiredService<ILogger<DevSeederMarker>>();

        var accounts = config
            .GetSection("DevSeed:Accounts")
            .Get<List<DevSeedAccountConfig>>();

        if (accounts is null || accounts.Count == 0)
        {
            logger.LogInformation("[DevSeeder] No seed accounts configured — skipping.");
            return;
        }

        foreach (var account in accounts)
        {
            if (string.IsNullOrWhiteSpace(account.Email) ||
                string.IsNullOrWhiteSpace(account.Password) ||
                string.IsNullOrWhiteSpace(account.Role))
            {
                logger.LogWarning("[DevSeeder] Skipping incomplete seed entry (missing Email, Password, or Role).");
                continue;
            }

            if (!Enum.TryParse<UserRole>(account.Role, ignoreCase: true, out var role))
            {
                logger.LogWarning("[DevSeeder] Unknown role '{Role}' — skipping {Email}.", account.Role, account.Email);
                continue;
            }

            var existing = await userRepo.FindByEmailAsync(account.Email);
            if (existing is not null)
            {
                logger.LogInformation("[DevSeeder] {Email} already exists ({Role}) — skipping.", account.Email, existing.Role);
                continue;
            }

            var user = new User
            {
                Email        = account.Email.Trim().ToLowerInvariant(),
                PasswordHash = hasher.Hash(account.Password),
                Role         = role,
                Status       = UserStatus.Active,
                DisplayName  = account.DisplayName ?? $"{role} User",
            };

            await userRepo.CreateAsync(user);
            logger.LogInformation("[DevSeeder] Created {Role} account: {Email}", role, user.Email);
        }
    }
}

/// <summary>Marker type used only for ILogger category naming.</summary>
internal sealed class DevSeederMarker;

/// <summary>Strongly-typed config entry for a single seed account.</summary>
public sealed class DevSeedAccountConfig
{
    public string Email       { get; set; } = string.Empty;
    public string Password    { get; set; } = string.Empty;
    public string Role        { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}
