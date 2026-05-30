using System.Data;
using BiyaHero.Api.Domain;
using Dapper;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// PostgreSQL repository for the users table using Dapper.
/// Extends BasePostgresRepository for generic CRUD and adds
/// email-based lookups and user-management operations required by Auth_Service.
/// Requirements: 5.1, 5.5, 5.8, 5.9, 10.3
/// </summary>
public class UserRepository : BasePostgresRepository<User>, IUserRepository
{
    private static readonly HashSet<string> ValidLanguages = new(StringComparer.OrdinalIgnoreCase) { "en", "fil" };

    private readonly IDbConnectionFactory _connectionFactory;

    // ─── Enum → Postgres snake_case helpers ──────────────────────────────

    /// <summary>Converts PascalCase C# enum name to snake_case for Postgres enums.</summary>
    private static string ToSnakeCase(string value)
    {
        // Insert underscore before each uppercase letter (except the first), then lowercase all
        var result = System.Text.RegularExpressions.Regex.Replace(value, "(?<!^)([A-Z])", "_$1");
        return result.ToLowerInvariant();
    }

    private static string RoleToDb(UserRole role) => ToSnakeCase(role.ToString());
    private static string StatusToDb(UserStatus status) => ToSnakeCase(status.ToString());

    public UserRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // ─── Table Configuration ──────────────────────────────────────────────

    protected override string TableName => "users";

    // ─── Mapping ──────────────────────────────────────────────────────────

    protected override User MapToEntity(dynamic row)
    {
        return new User(
            id: (Guid)row.id,
            createdAt: (DateTime)row.created_at,
            updatedAt: (DateTime)row.updated_at,
            email: (string)row.email,
            passwordHash: (string)row.password_hash,
            role: ParseRole((string)row.role),
            status: ParseStatus((string)row.status),
            displayName: (string)row.display_name,
            languagePreference: (string)row.language_preference
        );
    }

    /// <summary>Converts Postgres snake_case enum value back to C# PascalCase enum.</summary>
    private static UserRole ParseRole(string value)
    {
        // super_admin → SuperAdmin, commuter → Commuter, etc.
        var pascal = System.Text.RegularExpressions.Regex.Replace(
            value, @"(^|_)([a-z])", m => m.Groups[2].Value.ToUpperInvariant());
        return Enum.Parse<UserRole>(pascal, ignoreCase: true);
    }

    private static UserStatus ParseStatus(string value)
    {
        var pascal = System.Text.RegularExpressions.Regex.Replace(
            value, @"(^|_)([a-z])", m => m.Groups[2].Value.ToUpperInvariant());
        return Enum.Parse<UserStatus>(pascal, ignoreCase: true);
    }

    // ─── INSERT ───────────────────────────────────────────────────────────

    protected override string GetInsertSql()
    {
        return """
            INSERT INTO users (id, email, password_hash, role, status, display_name, language_preference, created_at, updated_at)
            VALUES (@Id, @Email, @PasswordHash, @Role::user_role, @Status::user_status, @DisplayName, @LanguagePreference, @CreatedAt, @UpdatedAt)
            """;
    }

    protected override object GetInsertParameters(User entity)
    {
        return new
        {
            entity.Id,
            entity.Email,
            entity.PasswordHash,
            Role = RoleToDb(entity.Role),
            Status = StatusToDb(entity.Status),
            entity.DisplayName,
            entity.LanguagePreference,
            entity.CreatedAt,
            entity.UpdatedAt
        };
    }

    // ─── UPDATE ───────────────────────────────────────────────────────────

    protected override string GetUpdateSql()
    {
        return """
            UPDATE users
            SET email = @Email,
                password_hash = @PasswordHash,
                role = @Role::user_role,
                status = @Status::user_status,
                display_name = @DisplayName,
                language_preference = @LanguagePreference,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """;
    }

    protected override object GetUpdateParameters(User entity)
    {
        return new
        {
            entity.Id,
            entity.Email,
            entity.PasswordHash,
            Role = RoleToDb(entity.Role),
            Status = StatusToDb(entity.Status),
            entity.DisplayName,
            entity.LanguagePreference,
            entity.UpdatedAt
        };
    }

    // ─── IUserRepository Methods ──────────────────────────────────────────

    /// <inheritdoc />
    public async Task<User?> FindByEmailAsync(string email)
    {
        await using var connection = (await _connectionFactory.CreateConnectionAsync()) as IAsyncDisposable
            ?? throw new InvalidOperationException("Connection does not support async disposal");

        var dbConnection = (IDbConnection)connection;

        // The email column uses citext, so comparison is inherently case-insensitive.
        // No LOWER() call needed — citext handles it at the storage/index level.
        const string sql = """
            SELECT id, email, password_hash, role, status, display_name, language_preference, created_at, updated_at
            FROM users
            WHERE email = @Email
            LIMIT 1
            """;

        var row = await dbConnection.QueryFirstOrDefaultAsync(sql, new { Email = email });

        return row is null ? null : MapToEntity(row);
    }

    /// <inheritdoc />
    public async Task<bool> EmailExistsAsync(string email)
    {
        // citext column — equality comparison is case-insensitive without LOWER()
        var count = await ScalarAsync<int>(
            "SELECT COUNT(1) FROM users WHERE email = @Email",
            new { Email = email });

        return count > 0;
    }

    /// <inheritdoc />
    public async Task<User> UpdateLanguagePreferenceAsync(Guid userId, string languagePreference)
    {
        if (!ValidLanguages.Contains(languagePreference))
        {
            throw new ArgumentException(
                $"Invalid language preference '{languagePreference}'. Must be 'en' or 'fil'.",
                nameof(languagePreference));
        }

        var normalizedPreference = languagePreference.ToLowerInvariant();
        var now = DateTime.UtcNow;

        await using var connection = (await _connectionFactory.CreateConnectionAsync()) as IAsyncDisposable
            ?? throw new InvalidOperationException("Connection does not support async disposal");

        var dbConnection = (IDbConnection)connection;

        const string sql = """
            UPDATE users
            SET language_preference = @LanguagePreference,
                updated_at = @UpdatedAt
            WHERE id = @Id
            RETURNING id, email, password_hash, role, status, display_name, language_preference, created_at, updated_at
            """;

        var row = await dbConnection.QueryFirstOrDefaultAsync(sql, new
        {
            Id = userId,
            LanguagePreference = normalizedPreference,
            UpdatedAt = now
        });

        if (row is null)
        {
            throw new InvalidOperationException($"User with id {userId} not found.");
        }

        return MapToEntity(row);
    }

    /// <inheritdoc />
    public async Task<User> SuspendAsync(Guid userId)
    {
        var now = DateTime.UtcNow;

        await using var connection = (await _connectionFactory.CreateConnectionAsync()) as IAsyncDisposable
            ?? throw new InvalidOperationException("Connection does not support async disposal");

        var dbConnection = (IDbConnection)connection;

        const string sql = """
            UPDATE users
            SET status = @Status::user_status,
                updated_at = @UpdatedAt
            WHERE id = @Id
            RETURNING id, email, password_hash, role, status, display_name, language_preference, created_at, updated_at
            """;

        var row = await dbConnection.QueryFirstOrDefaultAsync(sql, new
        {
            Id = userId,
            Status = StatusToDb(UserStatus.Suspended),
            UpdatedAt = now
        });

        if (row is null)
        {
            throw new InvalidOperationException($"User with id {userId} not found.");
        }

        return MapToEntity(row);
    }

    /// <inheritdoc />
    public async Task<User> ChangeRoleAsync(Guid userId, UserRole newRole)
    {
        var now = DateTime.UtcNow;

        await using var connection = (await _connectionFactory.CreateConnectionAsync()) as IAsyncDisposable
            ?? throw new InvalidOperationException("Connection does not support async disposal");

        var dbConnection = (IDbConnection)connection;

        const string sql = """
            UPDATE users
            SET role = @Role::user_role,
                updated_at = @UpdatedAt
            WHERE id = @Id
            RETURNING id, email, password_hash, role, status, display_name, language_preference, created_at, updated_at
            """;

        var row = await dbConnection.QueryFirstOrDefaultAsync(sql, new
        {
            Id = userId,
            Role = RoleToDb(newRole),
            UpdatedAt = now
        });

        if (row is null)
        {
            throw new InvalidOperationException($"User with id {userId} not found.");
        }

        return MapToEntity(row);
    }
}
