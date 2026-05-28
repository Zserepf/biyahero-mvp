namespace BiyaHero.Api.Services;

/// <summary>
/// Abstraction for sending transactional emails.
/// MVP implementation uses AWS SES; can be swapped for other providers.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends a verification email containing a link with the given token.
    /// The link format is: {baseUrl}/verify-email?token={verificationToken}
    /// </summary>
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="verificationToken">The URL-safe verification token.</param>
    Task SendVerificationEmailAsync(string toEmail, string verificationToken);
}
