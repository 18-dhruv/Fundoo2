using Fundoo.Notification.Application.DTO;
using Fundoo.Notification.Application.Templates;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Fundoo.Notification.Application.Abstractions;
using Fundoo.Observability;
using System.Diagnostics;

namespace Fundoo.Notification.Api.Services;

public sealed class SmtpEmailService(
    IConfiguration configuration,
    SmtpBulkhead bulkhead) : INotificationService
{
    public async Task SendNoteActivityNotificationAsync(
        NoteActivityNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var body = NoteActivityEmailTemplate.Build(
            request.Title, request.EventType, request.NoteId);
        await SendEmailAsync(
            request.RecipientEmail,
            "Fundoo Notes - Note activity",
            body,
            "note_activity",
            cancellationToken);
    }

    public async Task SendPasswordResetNotificationAsync(
    PasswordResetNotificationRequest request)
    {
        await bulkhead.ExecuteAsync(async () =>
        {
            var body = PasswordResetEmailTemplate.Build(
                "User",
                request.ResetToken);

            await SendEmailAsync(
                request.RecipientEmail,
                "Fundoo Notes - Password Reset",
                body,
                "password_reset");
        });
    }

    public async Task SendWelcomeNotificationAsync(
        WelcomeNotificationRequest request)
    {
        var body = WelcomeEmailTemplate.Build(
            request.RecipientName);

        await SendEmailAsync(
            request.RecipientEmail,
            "Welcome to Fundoo Notes!",
            body,
            "welcome");
    }

    private async Task SendEmailAsync(
        string recipientEmail,
        string subject,
        string body,
        string template,
        CancellationToken cancellationToken = default)
    {
        var host = GetRequiredSetting("Host");

        if (!int.TryParse(
                GetRequiredSetting("Port"),
                out var port))
        {
            throw new InvalidOperationException(
                "EmailSettings:Port must be a valid number.");
        }

        var senderEmail = GetRequiredSetting("SenderEmail");
        var senderName = configuration["EmailSettings:SenderName"]
                         ?? "Fundoo Notes";

        var username = GetRequiredSetting("Username");
        var password = GetRequiredSetting("Password");

        var message = new MimeMessage();

        message.From.Add(
            new MailboxAddress(senderName, senderEmail));

        message.To.Add(
            MailboxAddress.Parse(recipientEmail));

        message.Subject = subject;

        message.Body = new TextPart("html")
        {
            Text = body
        };

        // StartTls in every real environment; "None" only for a local catch-all SMTP server (Mailpit).
        var security = Enum.TryParse<SecureSocketOptions>(
            configuration["EmailSettings:SecureSocketOptions"], ignoreCase: true, out var parsed)
            ? parsed
            : SecureSocketOptions.StartTls;

        // MailKit has no instrumentation: one CLIENT span per delivery with a child span per SMTP phase.
        // Only the server, port and template name are recorded - never the recipient, subject body or credentials.
        using var activity = FundooTelemetry.Email.StartActivity("smtp send", ActivityKind.Client);
        activity?.SetTag("network.protocol.name", "smtp");
        activity?.SetTag("server.address", host);
        activity?.SetTag("server.port", port);
        activity?.SetTag("fundoo.email.template", template);
        var started = Stopwatch.GetTimestamp();
        var outcome = "failed";

        using var client = new SmtpClient();
        try
        {
            using (FundooTelemetry.Email.StartActivity("smtp connect"))
            {
                await client.ConnectAsync(host, port, security, cancellationToken);
            }

            using (FundooTelemetry.Email.StartActivity("smtp authenticate"))
            {
                await client.AuthenticateAsync(username, password, cancellationToken);
            }

            using (FundooTelemetry.Email.StartActivity("smtp data"))
            {
                await client.SendAsync(message, cancellationToken);
            }

            outcome = "sent";
        }
        catch (Exception exception)
        {
            activity.RecordError(exception);
            throw;
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, CancellationToken.None);
            }

            var tags = new TagList { { "fundoo.email.template", template }, { "outcome", outcome } };
            FundooTelemetry.EmailsSent.Add(1, tags);
            FundooTelemetry.EmailDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, tags);
        }
    }

    public async Task SendTwoFactorOtpNotificationAsync(
        TwoFactorOtpNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        await bulkhead.ExecuteAsync(async () =>
        {
            var body = TwoFactorOtpEmailTemplate.Build(
                request.RecipientName,
                request.OtpCode,
                request.ExpiryMinutes);

            await SendEmailAsync(
                request.RecipientEmail,
                "Fundoo Notes - Two-Factor Authentication OTP",
                body,
                "two_factor_otp",
                cancellationToken);
        });
    }


    private string GetRequiredSetting(string key)
    {
        return configuration[$"EmailSettings:{key}"]
               ?? throw new InvalidOperationException(
                   $"EmailSettings:{key} is missing.");
    }
}
