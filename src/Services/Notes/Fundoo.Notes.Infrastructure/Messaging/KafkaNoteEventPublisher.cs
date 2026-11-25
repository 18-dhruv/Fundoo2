using Confluent.Kafka;
using Fundoo.EventContracts.Events;
using Fundoo.Observability;
using Fundoo.Notes.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Fundoo.Notes.Infrastructure.Messaging;

public sealed class KafkaNoteEventPublisher : INoteEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _createdTopic;
    private readonly string _updatedTopic;
    private readonly ILogger<KafkaNoteEventPublisher> _logger;

    public KafkaNoteEventPublisher(
        IConfiguration configuration,
        ILogger<KafkaNoteEventPublisher> logger)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"];
        if (string.IsNullOrWhiteSpace(bootstrapServers))
            throw new InvalidOperationException("Kafka:BootstrapServers is missing.");

        _createdTopic = configuration["Kafka:Topics:NoteCreated"]
            ?? throw new InvalidOperationException("Kafka:Topics:NoteCreated is missing.");
        _updatedTopic = configuration["Kafka:Topics:NoteUpdated"]
            ?? throw new InvalidOperationException("Kafka:Topics:NoteUpdated is missing.");
        _logger = logger;

        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        }).Build();
    }

    public Task PublishCreatedAsync(NoteEvent noteEvent, CancellationToken cancellationToken) =>
        PublishAsync(_createdTopic, noteEvent, cancellationToken);

    public Task PublishUpdatedAsync(NoteEvent noteEvent, CancellationToken cancellationToken) =>
        PublishAsync(_updatedTopic, noteEvent, cancellationToken);

    private async Task PublishAsync(
        string topic,
        NoteEvent noteEvent,
        CancellationToken cancellationToken)
    {
        var key = noteEvent.NoteId.ToString();

        // PRODUCER span; its W3C trace context travels in the record headers so the
        // Notification consumer continues the same trace.
        using var activity = FundooTelemetry.StartKafkaPublish(topic, key);
        activity?.SetTag("fundoo.event.type", noteEvent.EventType);

        var headers = new Headers();
        FundooTelemetry.InjectContext(activity, (name, value) =>
        {
            headers.Remove(name);
            headers.Add(name, value);
        });

        try
        {
            var result = await _producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = key,
                Value = JsonSerializer.Serialize(noteEvent),
                Headers = headers
            }, cancellationToken);

            activity?.SetTag("messaging.destination.partition.id", result.Partition.Value.ToString());
            activity?.SetTag("messaging.kafka.offset", result.Offset.Value);
        }
        catch (Exception exception)
        {
            activity.RecordError(exception);
            _logger.LogError(exception,
                "Failed publishing {EventType} for note {NoteId} to {Topic}",
                noteEvent.EventType, noteEvent.NoteId, topic);
            throw;
        }
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
    }
}
