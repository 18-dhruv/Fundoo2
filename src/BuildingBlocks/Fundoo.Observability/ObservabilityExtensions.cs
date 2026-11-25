using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Fundoo.Observability;

public static class ObservabilityExtensions
{
    /// <summary>
    /// Sends traces, metrics and logs over OTLP (default <c>http://localhost:4317</c>, gRPC).
    /// Configured with the standard variables:
    /// <list type="bullet">
    /// <item><c>OTEL_EXPORTER_OTLP_ENDPOINT</c>, <c>OTEL_EXPORTER_OTLP_PROTOCOL</c>, <c>OTEL_EXPORTER_OTLP_HEADERS</c></item>
    /// <item><c>OTEL_SERVICE_NAME</c> (defaults to <paramref name="serviceName"/>), <c>OTEL_RESOURCE_ATTRIBUTES</c></item>
    /// <item><c>OTEL_METRIC_EXPORT_INTERVAL</c>, <c>OTEL_SDK_DISABLED=true</c> to turn everything off</item>
    /// <item><c>SERVICE_VERSION</c> (baked into the image by the Dockerfile) and <c>DEPLOYMENT_ENVIRONMENT</c></item>
    /// </list>
    /// If no collector is listening the exporters drop data quietly; the service keeps working.
    /// </summary>
    /// <param name="configureTracing">Service-specific extras, e.g. Redis instrumentation for the User API.</param>
    /// <param name="enrichServerSpan">Called when an incoming request completes, e.g. to name gateway spans by Ocelot route.</param>
    public static WebApplicationBuilder AddFundooObservability(
        this WebApplicationBuilder builder,
        string serviceName,
        Action<TracerProviderBuilder>? configureTracing = null,
        Action<Activity, HttpContext>? enrichServerSpan = null)
    {
        var config = builder.Configuration;
        if (string.Equals(config["OTEL_SDK_DISABLED"], "true", StringComparison.OrdinalIgnoreCase))
            return builder;

        var name = NonEmpty(config["OTEL_SERVICE_NAME"]) ?? serviceName;
        var version = NonEmpty(config["SERVICE_VERSION"])
            ?? Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "unknown";
        var environment = NonEmpty(config["DEPLOYMENT_ENVIRONMENT"]) ?? builder.Environment.EnvironmentName.ToLowerInvariant();

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(name, serviceVersion: version, serviceInstanceId: Environment.MachineName)
                .AddAttributes(new KeyValuePair<string, object>[]
                {
                    // SigNoz filters on "deployment.environment"; the newer semconv name is set too.
                    new("deployment.environment", environment),
                    new("deployment.environment.name", environment),
                }))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource("Fundoo.*")
                    // Npgsql has a built-in ActivitySource: covers raw Npgsql (Notes) and EF Core (User).
                    // It records the parameterised SQL text, never parameter values.
                    .AddSource("Npgsql")
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = IsTraced;
                        // Exceptions are recorded through RecordError so their messages get masked.
                        options.RecordException = false;
                        options.EnrichWithException = (activity, exception) => activity.RecordError(exception);
                        if (enrichServerSpan is not null)
                            options.EnrichWithHttpResponse = (activity, response) => enrichServerSpan(activity, response.HttpContext);
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = false;
                        options.EnrichWithException = (activity, exception) => activity.RecordError(exception);
                    });

                configureTracing?.Invoke(tracing);

                // Order matters: the redactor must see the span before the exporter does.
                tracing
                    .AddProcessor(new RedactingActivityProcessor())
                    .AddOtlpExporter();
            })
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()      // http.server.request.duration (+ Kestrel, routing)
                .AddHttpClientInstrumentation()      // http.client.request.duration
                .AddRuntimeInstrumentation()         // GC, thread pool, exceptions
                .AddMeter("Npgsql")                  // connection pool / command metrics
                .AddMeter(FundooTelemetry.MeterName)
                .AddOtlpExporter())
            .WithLogging(
                logging => logging
                    .AddProcessor(new RedactingLogProcessor())
                    .AddOtlpExporter(),
                options =>
                {
                    // Log records carry trace_id/span_id automatically (they are written inside the active span).
                    options.IncludeFormattedMessage = true;
                    options.IncludeScopes = true;
                });

        return builder;
    }

    private static bool IsTraced(HttpContext context)
    {
        var path = context.Request.Path;
        return !path.StartsWithSegments("/health")
            && !path.StartsWithSegments("/swagger")
            && !path.StartsWithSegments("/favicon.ico");
    }

    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
