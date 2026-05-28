using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Common.Exceptions;
using BiyaHero.Api.Repositories;

namespace BiyaHero.Api.Features.Auth.VerifyEmail;

/// <summary>
/// Business logic handler for email verification.
/// Validates the token, activates the user account, and invalidates the token.
/// Requirements: 5.2
/// </summary>
public sealed class VerifyEmailHandler
{
    private readonly IVerificationTokenStore _tokenStore;
    private readonly IUserRepository _userRepository;

    public VerifyEmailHandler(IVerificationTokenStore tokenStore, IUserRepository userRepository)
    {
        _tokenStore = tokenStore;
        _userRepository = userRepository;
    }

    /// <summary>
    /// Processes an email verification request.
    /// </summary>
    /// <param name="request">The verification request containing the token.</param>
    /// <returns>A response with a success message and the verified email.</returns>
    /// <exception cref="ValidationException">Thrown when the token is missing or empty.</exception>
    /// <exception cref="InvalidTokenException">Thrown when the token is invalid or expired.</exception>
    public async Task<VerifyEmailResponse> HandleAsync(VerifyEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            throw new ValidationException("Token is required.");
        }

        var userId = await _tokenStore.ValidateAndConsumeTokenAsync(request.Token);

        if (userId is null)
        {
            throw new InvalidTokenException();
        }

        var user = await _userRepository.FindByIdAsync(userId.Value);

        if (user is null)
        {
            throw new InvalidTokenException();
        }

        // Activate the user account
        user.Status = UserStatus.Active;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        return new VerifyEmailResponse(
            Message: "Email verified successfully.",
            Email: user.Email
        );
    }
}
