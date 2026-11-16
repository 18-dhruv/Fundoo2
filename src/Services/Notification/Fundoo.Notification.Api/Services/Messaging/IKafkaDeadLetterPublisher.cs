using Confluent.Kafka;

namespace Fundoo.Notification.Api.Services.Messaging;

public interface IKafkaDeadLetterPublisher
{
    Task PublishAsync(
        ConsumeResult<string, string> failedRecord,
        CancellationToken cancellationToken);
}
