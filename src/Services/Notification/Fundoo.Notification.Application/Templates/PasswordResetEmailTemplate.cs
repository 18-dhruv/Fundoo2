using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Fundoo.Notification.Application.Templates
{
    public static class PasswordResetEmailTemplate
    {
        public static string Build(
            string recipientName,
            string resetToken)
        {
            var safeName = WebUtility.HtmlEncode(recipientName);
            var safeToken = WebUtility.HtmlEncode(resetToken);

            return $"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="UTF-8">
                <title>Fundoo Password Reset</title>
            </head>
            <body style="font-family: Arial, sans-serif;">
                <h2>Password Reset Request</h2>

                <p>Hello {safeName},</p>

                <p>
                    We received a request to reset your
                    Fundoo Notes account password.
                </p>

                <p>Your password reset token is:</p>

                <div style="padding: 12px; background: #f2f2f2;">
                    <strong>{safeToken}</strong>
                </div>

                <p>This token expires in 15 minutes.</p>

                <p>
                    If you did not request a password reset,
                    you can safely ignore this email.
                </p>

                <p>Regards,<br>Fundoo Notes Team</p>
            </body>
            </html>
            """;
        }
    }
}