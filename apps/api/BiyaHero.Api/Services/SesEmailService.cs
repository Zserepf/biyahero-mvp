using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;

namespace BiyaHero.Api.Services;

/// <summary>
/// AWS SES implementation of IEmailService.
/// Sends transactional verification emails with a link containing the token.
/// The base URL and sender address are configured via IConfiguration.
/// </summary>
public sealed class SesEmailService : IEmailService
{
    private readonly IAmazonSimpleEmailService _sesClient;
    private readonly string _senderEmail;
    private readonly string _verificationBaseUrl;

    public SesEmailService(
        IAmazonSimpleEmailService sesClient,
        IConfiguration configuration)
    {
        _sesClient = sesClient;
        _senderEmail = configuration["Email:SenderAddress"] ?? "noreply@biyahero.app";
        _verificationBaseUrl = configuration["Email:VerificationBaseUrl"] ?? "https://biyahero.app";
    }

    /// <inheritdoc />
    public async Task SendVerificationEmailAsync(string toEmail, string verificationToken)
    {
        var verificationLink = $"{_verificationBaseUrl}/verify-email?token={verificationToken}";

        var request = new SendEmailRequest
        {
            Source = _senderEmail,
            Destination = new Destination
            {
                ToAddresses = new List<string> { toEmail }
            },
            Message = new Message
            {
                Subject = new Content("Verify your BiyaHero account"),
                Body = new Body
                {
                    Html = new Content(BuildHtmlBody(verificationLink)),
                    Text = new Content(BuildTextBody(verificationLink))
                }
            }
        };

        await _sesClient.SendEmailAsync(request);
    }

    private static string BuildHtmlBody(string verificationLink)
    {
        return $"""
            <html>
            <body style="font-family: Arial, sans-serif; padding: 20px;">
                <h2>Welcome to BiyaHero!</h2>
                <p>Please verify your email address by clicking the link below:</p>
                <p><a href="{verificationLink}" style="background-color: #2563eb; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; display: inline-block;">Verify Email</a></p>
                <p>Or copy and paste this link into your browser:</p>
                <p>{verificationLink}</p>
                <p>This link expires in 24 hours.</p>
                <p>If you did not create an account, you can safely ignore this email.</p>
            </body>
            </html>
            """;
    }

    private static string BuildTextBody(string verificationLink)
    {
        return $"""
            Welcome to BiyaHero!

            Please verify your email address by visiting the link below:

            {verificationLink}

            This link expires in 24 hours.

            If you did not create an account, you can safely ignore this email.
            """;
    }
}
