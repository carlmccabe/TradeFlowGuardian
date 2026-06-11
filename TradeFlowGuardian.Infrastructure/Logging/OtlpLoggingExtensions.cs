using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace TradeFlowGuardian.Infrastructure.Logging;

/// <summary>
/// Ships logs to an OTLP endpoint (Grafana Cloud → Loki) alongside the existing
/// console providers. Activates only when OTEL_EXPORTER_OTLP_ENDPOINT is set, so
/// local dev and environments without credentials are untouched.
/// </summary>
public static class OtlpLoggingExtensions
{
    /// <summary>
    /// Adds an OpenTelemetry log exporter when OTEL_EXPORTER_OTLP_ENDPOINT is set;
    /// no-op otherwise. Endpoint, auth headers, and protocol come from the standard
    /// OTEL_EXPORTER_OTLP_* environment variables (read by the exporter itself —
    /// this is the OpenTelemetry convention, not app config, hence no IOptions).
    /// Export is batched and fire-and-forget: a Grafana outage never blocks the app.
    /// </summary>
    public static ILoggingBuilder AddOtlpExportIfConfigured(this ILoggingBuilder logging, string serviceName)
    {
        var endpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        if (string.IsNullOrWhiteSpace(endpoint))
            return logging;

        // Distinguishes staging from production in Grafana queries.
        var deployEnv = Environment.GetEnvironmentVariable("RAILWAY_ENVIRONMENT_NAME")
                        ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                        ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                        ?? "unknown";

        logging.AddOpenTelemetry(opts =>
        {
            opts.IncludeScopes = true;
            opts.IncludeFormattedMessage = true;
            opts.ParseStateValues = true;
            opts.SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService(serviceName)
                .AddAttributes([new KeyValuePair<string, object>("deployment.environment", deployEnv)]));
            opts.AddOtlpExporter();
        });

        return logging;
    }
}
