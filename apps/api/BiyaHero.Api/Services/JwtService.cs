using System.Security.Claims;
using BiyaHero.Api.Domain;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BiyaHero.Api.Services;

/// <summary>
/// JWT issuer and verifier using HS256 with a KMS-backed signing key retrieved via ISecretService.
/// Access tokens expire in 24 hours; refresh tokens expire in 30 days.
/// Requirements: 5.3, 5.4, 5.7
/// </summary>
public sealed class JwtService : IJwtService
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    private readonly ISecretService _secretService;
    private readonly TimeProvider _timeProvider;

    public JwtService(ISecretService secretService, TimeProvider timeProvider)
    {
        _secretService = secretService;
        _timeProvider = timeProvider;
    }

    public async Task<string> GenerateAccessTokenAsync(User user, CancellationToken cancellationToken = default)
    {
        var key = await _secretService.GetJwtSigningKeyAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow();

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("role", user.Role.ToString()),
            }),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.Add(AccessTokenLifetime).UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature),
        };

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(descriptor);
    }

    public async Task<string> GenerateRefreshTokenAsync(User user, CancellationToken cancellationToken = default)
    {
        var key = await _secretService.GetJwtSigningKeyAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow();

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim("type", "refresh"),
            }),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.Add(RefreshTokenLifetime).UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature),
        };

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(descriptor);
    }

    public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var key = await _secretService.GetJwtSigningKeyAsync(cancellationToken);
        var validationParameters = BuildValidationParameters(key);

        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(token, validationParameters);

        if (!result.IsValid)
            return null;

        return new ClaimsPrincipal(result.ClaimsIdentity);
    }

    public async Task<Guid?> GetUserIdFromTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var principal = await ValidateTokenAsync(token, cancellationToken);
        if (principal == null)
            return null;

        var subClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                    ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (subClaim != null && Guid.TryParse(subClaim, out var userId))
            return userId;

        return null;
    }

    public async Task<JwtValidationResult> ValidateTokenDetailedAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return JwtValidationResult.Failure("Token is missing or empty.");

        var key = await _secretService.GetJwtSigningKeyAsync(cancellationToken);
        var validationParameters = BuildValidationParameters(key);

        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(token, validationParameters);

        if (!result.IsValid)
        {
            var message = result.Exception switch
            {
                SecurityTokenExpiredException => "Token has expired.",
                SecurityTokenInvalidSignatureException => "Token signature is invalid.",
                _ => result.Exception?.Message ?? "Token validation failed.",
            };
            return JwtValidationResult.Failure(message);
        }

        var subClaim = result.Claims.TryGetValue(JwtRegisteredClaimNames.Sub, out var subObj) ? subObj?.ToString() : null;
        if (subClaim == null || !Guid.TryParse(subClaim, out var userId))
            return JwtValidationResult.Failure("Token is missing required 'sub' claim.");

        var email = result.Claims.TryGetValue(JwtRegisteredClaimNames.Email, out var emailObj) ? emailObj?.ToString() : null;
        var role = result.Claims.TryGetValue("role", out var roleObj) ? roleObj?.ToString() : null;
        var tokenType = result.Claims.TryGetValue("type", out var typeObj) ? typeObj?.ToString() : null;

        return JwtValidationResult.Success(userId, email, role, tokenType);
    }

    private TokenValidationParameters BuildValidationParameters(byte[] key)
    {
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            LifetimeValidator = (notBefore, expires, _, _) =>
            {
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                if (expires == null) return false;
                return expires > now;
            },
        };
    }
}
