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
            role: Enum.Parse<UserRole>((string)row.role, ignoreCase: true),
            status: Enum.Parse<UserStatus>((string)row.status, ignoreCase: true),
            displayName: (string)row.display_name,
            languagePreference: (string)row.language_preference
        );
    }

    // ─── INSERT ───────────────────────────────────────────────────────────

    protected override string GetInsertSql()
    {
        return """
            INSERT INTO users (id, email, password_hash, role, status, display_name, language_preference, created_at, updated_at)
            VALUES (@Id, @Email, @PasswordHash, @Role, @Status, @DisplayName, @LanguagePreference, @CreatedAt, @UpdatedAt)
            """;
    }

    protected override object GetInsertParameters(User entity)
    {
        return new
        {
            entity.Id,
            entity.Email,
            entity.PasswordHash,
            Role = entity.Role.ToString(),
            Status = entity.Status.ToString(),
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
                role = @Role,
                status = @Status,
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
            Role = entity.Role.ToString(),
            Status = entity.Status.ToString(),
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
            SET status = @Status,
                updated_at = @UpdatedAt
            WHERE id = @Id
            RETURNING id, email, password_hash, role, status, display_name, language_preference, created_at, updated_at
            """;

        var row = await dbConnection.QueryFirstOrDefaultAsync(sql, new
        {
            Id = userId,
            Status = UserStatus.Suspended.ToString(),
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
            SET role = @Role,
                updated_at = @UpdatedAt
            WHERE id = @Id
            RETURNING id, email, password_hash, role, status, display_name, language_preference, created_at, updated_at
            """;

        var row = await dbConnection.QueryFirstOrDefaultAsync(sql, new
        {
            Id = userId,
            Role = newRole.ToString(),
            UpdatedAt = now
        });

        if (row is null)
        {
            throw new InvalidOperationException($"User with id {userId} not found.");
        }

        return MapToEntity(row);
    }
}
