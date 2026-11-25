using System.Diagnostics;
using Fundoo.Observability;
using NUnit.Framework;

namespace Fundoo.UnitTests;

[TestFixture]
public sealed class SensitiveDataRedactorTests
{
    [TestCase("http.request.header.authorization")]
    [TestCase("http.request.header.cookie")]
    [TestCase("http.response.header.set-cookie")]
    [TestCase("db.connection_string")]
    [TestCase("db.query.parameter.userId")]
    [TestCase("http.request.body")]
    [TestCase("Password")]
    [TestCase("ResetToken")]
    [TestCase("OtpCode")]
    [TestCase("http.request.header.x-api-key")]
    public void ShouldDrop_SensitiveKeys(string key) =>
        Assert.That(SensitiveDataRedactor.ShouldDrop(key), Is.True);

    [TestCase("http.route")]
    [TestCase("db.statement")]
    [TestCase("messaging.kafka.message.key")]
    [TestCase("server.address")]
    [TestCase("UserId")]
    public void ShouldDrop_KeepsOrdinaryKeys(string key) =>
        Assert.That(SensitiveDataRedactor.ShouldDrop(key), Is.False);

    [Test]
    public void RedactValue_StripsQueryStringFromUrls()
    {
        var value = SensitiveDataRedactor.RedactValue("url.full", "http://user-api/signin-google?code=abc&state=xyz");
        Assert.That(value, Is.EqualTo("http://user-api/signin-google?[REDACTED]"));
        Assert.That(SensitiveDataRedactor.RedactValue("url.query", "code=abc"), Is.EqualTo("[REDACTED]"));
    }

    [Test]
    public void RedactValue_ReplacesSqlStringLiterals()
    {
        var value = SensitiveDataRedactor.RedactValue(
            "db.statement", "SELECT * FROM users WHERE email = 'a@b.com' AND name = 'O''Brien' AND id = $1");
        Assert.That(value, Is.EqualTo("SELECT * FROM users WHERE email = ? AND name = ? AND id = $1"));
    }

    [Test]
    public void MaskFreeText_MasksEmailsJwtsAndBearerTokens()
    {
        const string jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjMifQ.c2lnbmF0dXJlLXZhbHVl";
        var masked = SensitiveDataRedactor.MaskFreeText(
            $"550 <someone@example.com> unknown; token {jwt}; header Bearer abc.def-123");

        Assert.That(masked, Does.Not.Contain("someone@example.com"));
        Assert.That(masked, Does.Not.Contain(jwt));
        Assert.That(masked, Does.Not.Contain("abc.def-123"));
        Assert.That(masked, Does.Contain("[EMAIL]"));
    }

    [Test]
    public void MaskFreeText_StripsQueryStringsFromUrlsInLogMessages()
    {
        var masked = SensitiveDataRedactor.MaskFreeText(
            "Request starting HTTP/1.1 GET http://localhost:5100/api/notes/42?debug=secret - - -");
        Assert.That(masked, Is.EqualTo("Request starting HTTP/1.1 GET http://localhost:5100/api/notes/42?[REDACTED] - - -"));
        Assert.That(SensitiveDataRedactor.MaskFreeText("Is it healthy?"), Is.EqualTo("Is it healthy?"));
        Assert.That(SensitiveDataRedactor.RedactValue("QueryString", "?debug=secret"), Is.EqualTo("[REDACTED]"));
    }

    [Test]
    public void ActivityProcessor_RemovesAndMasksTagsBeforeExport()
    {
        using var listener = ListenToAll();
        using var source = new ActivitySource("Fundoo.Tests");
        using var activity = source.StartActivity("test")!;
        activity.SetTag("db.connection_string", "Host=x;Password=secret");
        activity.SetTag("http.request.header.authorization", "Bearer x");
        activity.SetTag("url.full", "https://gw/api/auth/reset?token=abc");
        activity.SetTag("enduser.email", "someone@example.com");
        activity.SetTag("http.route", "api/notes/{id}");
        activity.SetStatus(ActivityStatusCode.Error, "failed for someone@example.com");

        new RedactingActivityProcessor().OnEnd(activity);

        Assert.That(activity.GetTagItem("db.connection_string"), Is.Null);
        Assert.That(activity.GetTagItem("http.request.header.authorization"), Is.Null);
        Assert.That(activity.GetTagItem("url.full"), Is.EqualTo("https://gw/api/auth/reset?[REDACTED]"));
        Assert.That(activity.GetTagItem("enduser.email"), Is.EqualTo("[EMAIL]"));
        Assert.That(activity.GetTagItem("http.route"), Is.EqualTo("api/notes/{id}"));
        Assert.That(activity.StatusDescription, Does.Not.Contain("someone@example.com"));
    }

    [Test]
    public void KafkaPropagation_ConsumerSpanContinuesProducerTrace()
    {
        using var listener = ListenToAll();
        var headers = new Dictionary<string, byte[]>();

        string producerTraceId, producerSpanId;
        using (var publish = FundooTelemetry.StartKafkaPublish("notes.created.v1", "note-1")!)
        {
            FundooTelemetry.InjectContext(publish, (name, value) => headers[name] = value);
            producerTraceId = publish.TraceId.ToString();
            producerSpanId = publish.SpanId.ToString();
            Assert.That(publish.Kind, Is.EqualTo(ActivityKind.Producer));
        }

        Assert.That(headers, Does.ContainKey("traceparent"));

        using var process = FundooTelemetry.StartKafkaProcess(
            "notes.created.v1", "fundoo-notifications-v1",
            name => headers.TryGetValue(name, out var v) ? v : null,
            "note-1", partition: 0, offset: 42)!;

        Assert.That(process.Kind, Is.EqualTo(ActivityKind.Consumer));
        Assert.That(process.TraceId.ToString(), Is.EqualTo(producerTraceId));
        Assert.That(process.ParentSpanId.ToString(), Is.EqualTo(producerSpanId));
        Assert.That(process.GetTagItem("messaging.destination.name"), Is.EqualTo("notes.created.v1"));
    }

    private static ActivityListener ListenToAll()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
