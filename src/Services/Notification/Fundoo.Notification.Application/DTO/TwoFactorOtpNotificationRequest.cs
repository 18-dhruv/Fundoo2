
namespace Fundoo.Notification.Application.DTO;

public sealed class TwoFactorOtpNotificationRequest
{
    public string RecipientEmail { get; set; } = string.Empty;

    public string RecipientName { get; set; } = string.Empty;

    public string OtpCode { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 10;
}
