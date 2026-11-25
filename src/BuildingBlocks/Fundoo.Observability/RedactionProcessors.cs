using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Fundoo.Observability;

/// <summary>Scrubs span attributes before export (runs ahead of the OTLP exporter).</summary>
public sealed class RedactingActivityProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        List<KeyValuePair<string, object?>>? changes = null;

        foreach (var tag in activity.TagObjects)
        {
            if (SensitiveDataRedactor.ShouldDrop(tag.Key))
            {
                (changes ??= new()).Add(new(tag.Key, null));
                continue;
            }

            var redacted = SensitiveDataRedactor.RedactValue(tag.Key, tag.Value);
            if (!ReferenceEquals(redacted, tag.Value) && !Equals(redacted, tag.Value))
                (changes ??= new()).Add(new(tag.Key, redacted));
        }

        if (changes is not null)
            foreach (var change in changes)
                activity.SetTag(change.Key, change.Value); // null removes the tag

        if (!string.IsNullOrEmpty(activity.StatusDescription))
        {
            var masked = SensitiveDataRedactor.MaskFreeText(activity.StatusDescription);
            if (!ReferenceEquals(masked, activity.StatusDescription))
                activity.SetStatus(activity.Status, masked);
        }
    }
}

/// <summary>Scrubs log attributes and the formatted message before export.</summary>
public sealed class RedactingLogProcessor : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord record)
    {
        var droppedAny = false;
        if (record.Attributes is { Count: > 0 } attributes)
        {
            List<KeyValuePair<string, object?>>? rewritten = null;
            for (var i = 0; i < attributes.Count; i++)
            {
                var (key, value) = attributes[i];
                if (key == "{OriginalFormat}")
                {
                    rewritten?.Add(attributes[i]);
                    continue;
                }

                object? newValue;
                var drop = SensitiveDataRedactor.ShouldDrop(key);
                droppedAny |= drop;
                newValue = drop ? SensitiveDataRedactor.Redacted : SensitiveDataRedactor.RedactValue(key, value);

                if (rewritten is null && !Equals(newValue, value))
                {
                    rewritten = new(attributes.Count);
                    for (var j = 0; j < i; j++)
                        rewritten.Add(attributes[j]);
                }

                rewritten?.Add(new(key, newValue));
            }

            if (rewritten is not null)
                record.Attributes = rewritten;
        }

        // A secret was interpolated into the message: fall back to the template ("... {Token} ...").
        if (droppedAny && record.Body is not null)
            record.FormattedMessage = record.Body;

        if (record.FormattedMessage is { Length: > 0 } message)
            record.FormattedMessage = SensitiveDataRedactor.MaskFreeText(message);
        if (record.Body is { Length: > 0 } body)
            record.Body = SensitiveDataRedactor.MaskFreeText(body);
    }
}
