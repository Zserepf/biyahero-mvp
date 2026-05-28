namespace BiyaHero.Api.Domain;

/// <summary>
/// Domain entity representing a registered user in the BiyaHero platform.
/// Inherits BaseDomain for standard OOP interface (Find, FindAll, Create, Where, Save, Update, Delete, Serialize).
/// Properties: Email, PasswordHash, Role, Status, DisplayName, LanguagePreference.
/// Requirements: 5.1, 5.2, 10.1, 10.3
/// </summary>
public class User : BaseDomain
{
    private static readonly HashSet<string> ValidLanguages = new(StringComparer.OrdinalIgnoreCase) { "en", "fil" };
    private const string DefaultLanguage = "fil";

    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Commuter;
    public UserStatus Status { get; set; } = UserStatus.PendingVerification;
    public string DisplayName { get; set; } = string.Empty;

    private string _languagePreference = DefaultLanguage;

    /// <summary>
    /// Language preference constrained to "en" or "fil". Defaults to "fil".
    /// Invalid values are silently replaced with the default.
    /// </summary>
    public string LanguagePreference
    {
        get => _languagePreference;
        set => _languagePreference = ValidLanguages.Contains(value) ? value.ToLowerInvariant() : DefaultLanguage;
    }

    /// <summary>
    /// Default constructor — creates a new User with generated Id and UTC timestamps.
    /// </summary>
    public User() : base() { }

    /// <summary>
    /// Full constructor for reconstituting a User from persisted data.
    /// </summary>
    public User(
        Guid id,
        DateTime createdAt,
        DateTime updatedAt,
        string email,
        string passwordHash,
        UserRole role,
        UserStatus status,
        string displayName,
        string languagePreference)
        : base(id, createdAt, updatedAt)
    {
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        Status = status;
        DisplayName = displayName;
        LanguagePreference = languagePreference;
    }

    /// <summary>
    /// Serialize this User to a JSON-compatible dictionary.
    /// PasswordHash is intentionally excluded from serialization for security.
    /// </summary>
    public override Dictionary<string, object?> Serialize()
    {
        var dict = base.Serialize();
        dict["email"] = Email;
        dict["role"] = Role.ToString();
        dict["status"] = Status.ToString();
        dict["displayName"] = DisplayName;
        dict["languagePreference"] = LanguagePreference;
        return dict;
    }

    /// <summary>
    /// Parse a serialized dictionary back into a User instance.
    /// This is the inverse of Serialize() and enables round-trip verification.
    /// Note: PasswordHash is not included in serialization output, so it defaults to empty on parse.
    /// </summary>
    public static User Parse(Dictionary<string, object?> data)
    {
        var id = Guid.Parse(data["id"]?.ToString() ?? throw new ArgumentException("Missing id"));
        var createdAt = DateTime.Parse(data["createdAt"]?.ToString() ?? throw new ArgumentException("Missing createdAt"));
        var updatedAt = DateTime.Parse(data["updatedAt"]?.ToString() ?? throw new ArgumentException("Missing updatedAt"));
        var email = data["email"]?.ToString() ?? string.Empty;
        var role = Enum.Parse<UserRole>(data["role"]?.ToString() ?? nameof(UserRole.Commuter), ignoreCase: true);
        var status = Enum.Parse<UserStatus>(data["status"]?.ToString() ?? nameof(UserStatus.PendingVerification), ignoreCase: true);
        var displayName = data["displayName"]?.ToString() ?? string.Empty;
        var languagePreference = data["languagePreference"]?.ToString() ?? DefaultLanguage;

        return new User(id, createdAt, updatedAt, email, string.Empty, role, status, displayName, languagePreference);
    }
}
