using BiyaHero.Api.Features.Common.Exceptions;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Features.Auth.Me;

/// <summary>
/// Business logic for authenticated user profile operations.
/// Handles GET /v1/auth/me and PATCH /v1/auth/me/language-preference.
/// </summary>
public sealed class MeHandler
{
    private static readonly HashSet<string> ValidLanguages = new(StringComparer.OrdinalIgnoreCase) { "en", "fil" };

    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;

    public MeHandler(IUserRepository userRepository, IJwtService jwtService)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
    }

    /// <summary>
    /// Returns the authenticated user's profile.
    /// Validates the access token and looks up the user by the token's sub claim.
    /// </summary>
    public async Task<MeResponse> GetMeAsync(string? authorizationHeader, CancellationToken ct = default)
    {
        var user = await ResolveAuthenticatedUserAsync(authorizationHeader, ct);

        return new MeResponse(
            Id: user.Id,
            Email: user.Email,
            Role: user.Role.ToString(),
            DisplayName: user.DisplayName,
            LanguagePreference: user.LanguagePreference);
    }

    /// <summary>
    /// Updates the authenticated user's language preference.
    /// Validates the language is "en" or "fil", then persists the change.
    /// </summary>
    public async Task<UpdateLanguagePreferenceResponse> UpdateLanguagePreferenceAsync(
        string? authorizationHeader,
        UpdateLanguagePreferenceRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.LanguagePreference) ||
            !ValidLanguages.Contains(request.LanguagePreference))
        {
            throw new ValidationException("languagePreference must be \"en\" or \"fil\".");
        }

        var user = await ResolveAuthenticatedUserAsync(authorizationHeader, ct);

        user.LanguagePreference = request.LanguagePreference;
        await _userRepository.UpdateAsync(user);

        return new UpdateLanguagePreferenceResponse(user.LanguagePreference);
    }

    /// <summary>
    /// Extracts and validates the Bearer token from the Authorization header,
    /// then resolves the user from the token's sub claim.
    /// </summary>
    private async Task<Domain.User> ResolveAuthenticatedUserAsync(string? authorizationHeader, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader) ||
            !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthenticatedException("Missing or invalid Authorization header.");
        }

        var token = authorizationHeader["Bearer ".Length..].Trim();

        var validationResult = await _jwtService.ValidateTokenDetailedAsync(token, ct);
        if (!validationResult.IsValid)
        {
            throw new UnauthenticatedException(validationResult.ErrorMessage ?? "Invalid token.");
        }

        var user = await _userRepository.FindByIdAsync(validationResult.UserId!.Value);
        if (user is null)
        {
            throw new UnauthenticatedException("User not found.");
        }

        return user;
    }
}
