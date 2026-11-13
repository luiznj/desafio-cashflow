using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CashFlow.Shared.Observability;

public static class ObservabilityExtensions
{
    public static IHostApplicationBuilder AddObservability(
        this IHostApplicationBuilder builder,
        string serviceName)
    {
        var traceEndpoint = builder.Configuration["Observability:TraceEndpoint"] ?? "http://localhost:4318";
        var logEndpoint   = builder.Configuration["Observability:LogEndpoint"]   ?? "http://localhost:3100/otlp";

        var resource = ResourceBuilder.CreateDefault()
            .AddService(serviceName, serviceVersion: "1.0.0")
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = builder.Environment.EnvironmentName
            });

        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .SetResourceBuilder(resource)
                .AddAspNetCoreInstrumentation(opts => opts.RecordException = true)
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddSource("MassTransit")
                .AddOtlpExporter(opts =>
                {
                    opts.Endpoint = new Uri(traceEndpoint);
                    opts.Protocol = OtlpExportProtocol.HttpProtobuf;
                }))
            .WithMetrics(metrics => metrics
                .SetResourceBuilder(resource)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddPrometheusExporter());

        // Mantém o provider de console padrão e adiciona OTLP para Loki
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.SetResourceBuilder(resource);
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.AddOtlpExporter(opts =>
            {
                opts.Endpoint = new Uri(logEndpoint);
                opts.Protocol = OtlpExportProtocol.HttpProtobuf;
            });
        });

        return builder;
    }
}
