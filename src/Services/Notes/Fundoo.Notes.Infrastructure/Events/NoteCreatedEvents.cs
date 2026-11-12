
namespace Fundoo.Notes.Infrastructure.Events;

public class NoteCreatedEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();

    public string EventType { get; set; } = "notes.created.v1";

    public int Version { get; set; } = 1;

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public Guid NoteId { get; set; }

    public Guid UserId { get; set; }

    public string RecipientEmail { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
}
