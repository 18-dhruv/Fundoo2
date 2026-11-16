namespace Fundoo.Notification.Application.DTO;

public sealed record NoteActivityNotificationRequest(
    string RecipientEmail,
    Guid NoteId,
    string Title,
    string EventType);
