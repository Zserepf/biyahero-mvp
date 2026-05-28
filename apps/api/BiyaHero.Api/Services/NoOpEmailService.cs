namespace BiyaHero.Api.Services;

/// <summary>
/// No-op email service for local development without SES.
/// Logs the verification token to console instead of sending email.
/// </summary>
public sealed class NoOpEmailService : IEmailService
{
    public Task SendVerificationEmailAsync(string toEmail, string verificationToken)
    {
        Console.WriteLine($"[DEV] Verification email for {toEmail}: token={verificationToken}");
        return Task.CompletedTask;
    }
}
