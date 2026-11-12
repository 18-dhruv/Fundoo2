
using System.Net;

namespace Fundoo.Notification.Application.Templates;

public static class TwoFactorOtpEmailTemplate
{
    public static string Build(
        string recipientName,
        string otpCode,
        int expiryMinutes)
    {
        var safeName = WebUtility.HtmlEncode(recipientName);
        var safeOtp = WebUtility.HtmlEncode(otpCode);

        return $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family: Arial, sans-serif;
                         background-color: #f4f4f4;
                         padding: 20px;">

                <div style="max-width: 500px;
                            margin: auto;
                            background: white;
                            padding: 30px;
                            border-radius: 10px;">

                    <h2 style="color: #333;">
                        Fundoo Notes - Two-Factor Authentication
                    </h2>

                    <p>Hello {safeName},</p>

                    <p>
                        Your OTP for two-factor authentication is:
                    </p>

                    <h1 style="color: #2563eb;
                               letter-spacing: 8px;">
                        {safeOtp}
                    </h1>

                    <p>
                        This OTP expires in {expiryMinutes} minutes.
                    </p>

                    <p>
                        Do not share this OTP with anyone.
                    </p>

                    <hr />

                    <p style="color: gray; font-size: 12px;">
                        If you did not request this code,
                        please ignore this email.
                    </p>

                </div>
            </body>
            </html>
            """;
    }
}
