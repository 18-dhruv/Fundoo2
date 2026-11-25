using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace Fundoo.Observability;

/// <summary>
/// Activity sources and meters for the code paths that have no auto-instrumentation
/// (Kafka via Confluent.Kafka, SMTP via MailKit). Everything named "Fundoo.*" is exported.
/// </summary>
public static class FundooTelemetry
{
    public const string MessagingSourceName = "Fundoo.Messaging";
    public const string EmailSourceName = "Fundoo.Email";
    public const string MeterName = "Fundoo";

    public static readonly ActivitySource Messaging = new(MessagingSourceName);
    public static readonly ActivitySource Email = new(EmailSourceName);
    public static readonly Meter Meter = new(MeterName);

    /// <summary>Kafka records handled by a consumer, tagged with topic and outcome (processed / dead_lettered / retried).</summary>
    public static readonly Counter<long> MessagesConsumed =
        Meter.CreateCounter<long>("fundoo.messaging.consumed", unit: "{message}", description: "Kafka records handled by Fundoo consumers.");

    /// <summary>E-mails attempted, tagged with template and outcome (sent / failed).</summary>
    public static readonly Counter<long> EmailsSent =
        Meter.CreateCounter<long>("fundoo.email.sent", unit: "{email}", description: "E-mails attempted by the Notification API.");

    /// <summary>Time spent on one SMTP delivery (connect + authenticate + send).</summary>
    public static readonly Histogram<double> EmailDuration =
        Meter.CreateHistogram<double>("fundoo.email.duration", unit: "s", description: "Duration of SMTP deliveries.");

    // W3C trace context + baggage, set explicitly so it works even before the SDK sets the global default.
    private static readonly TextMapPropagator Propagator = new CompositeTextMapPropagator(new TextMapPropagator[]
    {
        new TraceContextPropagator(),
        new BaggagePropagator(),
    });

    /// <summary>Starts a PRODUCER span for publishing one Kafka record.</summary>
    public static Activity? StartKafkaPublish(string topic, string? messageKey)
    {
        var activity = Messaging.StartActivity($"{topic} publish", ActivityKind.Producer);
        if (activity is null)
            return null;

        activity.SetTag("messaging.system", "kafka");
        activity.SetTag("messaging.operation.type", "send");
        activity.SetTag("messaging.operation.name", "publish");
        activity.SetTag("messaging.destination.name", topic);
        if (messageKey is not null)
            activity.SetTag("messaging.kafka.message.key", messageKey);
        return activity;
    }

    /// <summary>
    /// Writes the trace context of <paramref name="activity"/> (or the current one) into message headers.
    /// <paramref name="setHeader"/> must replace an existing header of the same name.
    /// </summary>
    public static void InjectContext(Activity? activity, Action<string, byte[]> setHeader)
    {
        activity ??= Activity.Current;
        if (activity is null)
            return;

        Propagator.Inject(
            new PropagationContext(activity.Context, Baggage.Current),
            setHeader,
            static (carrier, key, value) => carrier(key, Encoding.UTF8.GetBytes(value)));
    }

    /// <summary>
    /// Starts a CONSUMER span for processing one Kafka record, parented to the producer's span found in
    /// the headers so the asynchronous hop stays in the same trace.
    /// </summary>
    public static Activity? StartKafkaProcess(
        string topic,
        string consumerGroup,
        Func<string, byte[]?> getHeader,
        string? messageKey,
        int partition,
        long offset)
    {
        var parent = Propagator.Extract(default, getHeader, static (carrier, key) =>
        {
            var bytes = carrier(key);
            return bytes is null ? Array.Empty<string>() : new[] { Encoding.UTF8.GetString(bytes) };
        });
        Baggage.Current = parent.Baggage;

        var activity = Messaging.StartActivity(
            $"{topic} process",
            ActivityKind.Consumer,
            parent.ActivityContext);
        if (activity is null)
            return null;

        activity.SetTag("messaging.system", "kafka");
        activity.SetTag("messaging.operation.type", "process");
        activity.SetTag("messaging.operation.name", "process");
        activity.SetTag("messaging.destination.name", topic);
        activity.SetTag("messaging.consumer.group.name", consumerGroup);
        activity.SetTag("messaging.destination.partition.id", partition.ToString());
        activity.SetTag("messaging.kafka.offset", offset);
        if (messageKey is not null)
            activity.SetTag("messaging.kafka.message.key", messageKey);
        return activity;
    }

    /// <summary>
    /// Marks the span as failed and records the exception as an "exception" span event. The message and
    /// stack trace are passed through <see cref="SensitiveDataRedactor.MaskFreeText"/> first, because
    /// exception messages (e.g. SMTP "550 &lt;someone@example.com&gt; unknown") can carry e-mail addresses.
    /// </summary>
    public static void RecordError(this Activity? activity, Exception exception)
    {
        if (activity is null)
            return;

        var type = exception.GetType().FullName ?? exception.GetType().Name;
        activity.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
        activity.SetTag("error.type", type);
        activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
        {
            { "exception.type", type },
            { "exception.message", SensitiveDataRedactor.MaskFreeText(exception.Message) },
            { "exception.stacktrace", SensitiveDataRedactor.MaskFreeText(exception.ToString()) },
        }));
    }
}
