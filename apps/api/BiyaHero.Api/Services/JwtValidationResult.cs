namespace BiyaHero.Api.Services;

/// <summary>
/// Result of JWT token validation containing parsed claims or error information.
/// </summary>
public sealed record JwtValidationResult
{
    public bool IsValid { get; init; }
    public Guid? UserId { get; init; }
    public string? Email { get; init; }
    public string? Role { get; init; }
    public string? TokenType { get; init; }
    public string? ErrorMessage { get; init; }

    public static JwtValidationResult Success(Guid userId, string? email, string? role, string? tokenType = null) =>
        new() { IsValid = true, UserId = userId, Email = email, Role = role, TokenType = tokenType };

    public static JwtValidationResult Failure(string errorMessage) =>
        new() { IsValid = false, ErrorMessage = errorMessage };
}
