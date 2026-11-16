namespace Fundoo.EventContracts.Events;

public sealed record NoteEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public string EventType { get; init; } = string.Empty;
    public int Version { get; init; } = 1;
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
    public Guid NoteId { get; init; }
    public Guid UserId { get; init; }
    public string RecipientEmail { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
}
