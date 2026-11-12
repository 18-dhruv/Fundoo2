using Confluent.Kafka;
using Fundoo.Notes.Infrastructure.Events;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace Fundoo.Notes.Infrastructure.Messaging;

public class KafkaProducerService
{
    private readonly IProducer<string, string> _producer;

    public KafkaProducerService(IConfiguration configuration)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            Acks = Acks.All,
            EnableIdempotence = true
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishNoteCreatedAsync(
        NoteCreatedEvent noteEvent,
        CancellationToken cancellationToken = default)
    {
        var topic = "notes.created.v1";

        var message = new Message<string, string>
        {
            Key = noteEvent.NoteId.ToString(),
            Value = JsonSerializer.Serialize(noteEvent)
        };

        await _producer.ProduceAsync(
            topic,
            message,
            cancellationToken);
    }
}