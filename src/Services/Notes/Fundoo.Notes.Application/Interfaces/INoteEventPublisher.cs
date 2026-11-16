using Fundoo.EventContracts.Events;

namespace Fundoo.Notes.Application.Interfaces;

public interface INoteEventPublisher
{
    Task PublishCreatedAsync(NoteEvent noteEvent, CancellationToken cancellationToken);
    Task PublishUpdatedAsync(NoteEvent noteEvent, CancellationToken cancellationToken);
}
