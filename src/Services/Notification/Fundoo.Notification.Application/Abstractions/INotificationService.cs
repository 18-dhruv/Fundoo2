
using Fundoo.Notification.Application.DTO;

namespace Fundoo.Notification.Application.Abstractions
{
    public interface INotificationService
    {
        Task SendPasswordResetNotificationAsync(
            PasswordResetNotificationRequest request);

        Task SendWelcomeNotificationAsync(
            WelcomeNotificationRequest request);

        Task SendTwoFactorOtpNotificationAsync(
            TwoFactorOtpNotificationRequest request,
            CancellationToken cancellationToken = default);

        Task SendNoteActivityNotificationAsync(
            NoteActivityNotificationRequest request,
            CancellationToken cancellationToken = default);
    }
}
