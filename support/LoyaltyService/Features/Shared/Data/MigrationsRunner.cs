using System.Diagnostics;
using System.Reflection;
using DbUp;
using Polly;
using Polly.Retry;
// ReSharper disable AccessToDisposedClosure

namespace ChaosWorkshop.LoyaltyService.Features.Shared.Data;

public sealed class MigrationsRunner(IConfiguration configuration, ILoggerFactory loggerFactory) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var activity = StartActivity("apply database migrations", ActivityKind.Internal);

        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = int.MaxValue,
                Delay = TimeSpan.FromSeconds(5),
                OnRetry = _ =>
                {
                    activity?.AddEvent(new ActivityEvent("retrying database migrations"));
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

        pipeline.Execute(_ =>
        {
            var connectionString = configuration.GetConnectionString("Default")!;

            EnsureDatabase.For.PostgresqlDatabase(connectionString);

            var upgrader = DeployChanges.To
                .PostgresqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
                .LogTo(loggerFactory)
                .JournalToPostgresqlTable("public", "schema_versions")
                .WithTransaction()
                .Build();

            var result = upgrader.PerformUpgrade();

            if (result.Successful)
            {
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            else
            {
                activity?.AddException(result.Error);
                activity?.SetStatus(ActivityStatusCode.Error);
                // stop the application from starting, and not gracefully
                throw result.Error;
            }
        }, cancellationToken);


        return Task.CompletedTask;
    }


    // no-op, as we only want to execute stuff at startup
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}