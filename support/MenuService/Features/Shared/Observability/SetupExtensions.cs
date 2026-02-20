using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ChaosWorkshop.MenuService.Features.Shared.Observability;

internal static class SetupExtensions
{
    internal static ILoggingBuilder AddConfiguredOpenTelemetry(this ILoggingBuilder builder)
        => builder
            .AddOpenTelemetry(options =>
            {
                var resourceBuilder = ResourceBuilder.CreateDefault();
                ConfigureResource(resourceBuilder);
                options.SetResourceBuilder(resourceBuilder);

                options.IncludeScopes = true;
                options.IncludeFormattedMessage = true;
                options.ParseStateValues = true;

                options.AddOtlpExporter();
            });

    internal static IServiceCollection AddConfiguredOpenTelemetry(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(ConfigureResource)
            .WithTracing(trace => trace
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddSource(ActivitySourceName)
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddNpgsqlInstrumentation()
                .AddOtlpExporter());
        return services;
    }

    private static void ConfigureResource(ResourceBuilder r) => r.AddService(
        serviceName: typeof(Program).Assembly.GetName().Name ?? throw new InvalidOperationException("Unable to determine assembly name"),
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
        serviceInstanceId: Environment.MachineName);
}