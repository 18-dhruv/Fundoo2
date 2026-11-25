using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Fundoo.Observability;

namespace Fundoo.Notification.Api.Services.Messaging;

public sealed class KafkaDeadLetterPublisher : IKafkaDeadLetterPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly IAdminClient _adminClient;
    private readonly string _topic;
    private readonly ILogger<KafkaDeadLetterPublisher> _logger;
    private readonly SemaphoreSlim _topicSetupLock = new(1, 1);
    private bool _topicEnsured;

    public KafkaDeadLetterPublisher(
        IConfiguration configuration,
        ILogger<KafkaDeadLetterPublisher> logger)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers is missing.");
        _topic = configuration["Kafka:DeadLetterTopic"]
            ?? throw new InvalidOperationException("Kafka:DeadLetterTopic is missing.");
        _logger = logger;

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        };
        _producer = new ProducerBuilder<string, string>(producerConfig).Build();
        _adminClient = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = bootstrapServers
        }).Build();
    }

    public async Task PublishAsync(
        ConsumeResult<string, string> failedRecord,
        CancellationToken cancellationToken)
    {
        await EnsureTopicExistsAsync(cancellationToken);

        var headers = new Headers
        {
            { "original-topic", System.Text.Encoding.UTF8.GetBytes(failedRecord.Topic) },
            { "original-partition", System.Text.Encoding.UTF8.GetBytes(failedRecord.Partition.Value.ToString()) },
            { "original-offset", System.Text.Encoding.UTF8.GetBytes(failedRecord.Offset.Value.ToString()) }
        };

        // PRODUCER span under the consumer's "process" span, so the dead-lettering shows up in the
        // original trace; the DLQ record carries that context for whoever inspects it later.
        using var activity = FundooTelemetry.StartKafkaPublish(_topic, failedRecord.Message.Key);
        activity?.SetTag("fundoo.dead_letter.original_topic", failedRecord.Topic);
        FundooTelemetry.InjectContext(activity, (name, value) =>
        {
            headers.Remove(name);
            headers.Add(name, value);
        });

        try
        {
            await _producer.ProduceAsync(_topic, new Message<string, string>
            {
                Key = failedRecord.Message.Key,
                Value = failedRecord.Message.Value,
                Headers = headers
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            activity.RecordError(exception);
            throw;
        }
    }

    private async Task EnsureTopicExistsAsync(CancellationToken cancellationToken)
    {
        if (_topicEnsured)
            return;

        await _topicSetupLock.WaitAsync(cancellationToken);
        try
        {
            if (_topicEnsured)
                return;

            try
            {
                await _adminClient.CreateTopicsAsync(new[]
                {
                    new TopicSpecification
                    {
                        Name = _topic,
                        NumPartitions = 1,
                        ReplicationFactor = 1
                    }
                });
                _logger.LogInformation("Created Kafka dead-letter topic {Topic}", _topic);
            }
            catch (CreateTopicsException exception) when (
                exception.Results.Any() &&
                exception.Results.All(result => result.Error.Code == ErrorCode.TopicAlreadyExists))
            {
                _logger.LogDebug("Kafka dead-letter topic {Topic} already exists", _topic);
            }

            _topicEnsured = true;
        }
        finally
        {
            _topicSetupLock.Release();
        }
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
        _adminClient.Dispose();
        _topicSetupLock.Dispose();
    }
}
