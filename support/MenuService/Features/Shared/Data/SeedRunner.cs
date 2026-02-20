using System.Diagnostics;
using Dapper;
using Npgsql;
using Polly;
using Polly.Retry;
// ReSharper disable AccessToDisposedClosure

namespace ChaosWorkshop.MenuService.Features.Shared.Data;

public sealed class SeedRunner(NpgsqlDataSource dataSource) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = StartActivity("seeding database", ActivityKind.Internal);

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

        await pipeline.ExecuteAsync(async ct =>
        {
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            await connection.ExecuteAsync(
                new CommandDefinition(
                    // lang=postgresql
                    """
                    INSERT INTO public.products (id, price)
                    VALUES 
                        ('burger', 10),
                        ('fries', 5),
                        ('coke', 2.5)
                    ON CONFLICT (id) DO NOTHING;
                    """,
                    cancellationToken: ct));
        }, stoppingToken);
    }
}