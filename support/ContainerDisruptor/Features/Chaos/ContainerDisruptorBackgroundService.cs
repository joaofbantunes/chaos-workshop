using System.Diagnostics;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Options;

namespace ChaosWorkshop.ContainerDisruptor.Features.Chaos;

public sealed class ContainerDisruptorSettings
{
    public TimeSpan IntervalBetweenDisruptions { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan MaxDisruptionDuration { get; init; } = TimeSpan.FromSeconds(10);
}

public sealed class ContainerDisruptorBackgroundService(
    IOptions<ContainerDisruptorSettings> settings,
    ILogger<ContainerDisruptorBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var dockerClient = new DockerClientConfiguration().CreateClient();
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(settings.Value.IntervalBetweenDisruptions, stoppingToken);
            await DisruptSomethingAsync(dockerClient, stoppingToken);
        }
    }

    private async Task DisruptSomethingAsync(DockerClient dockerClient, CancellationToken ct)
    {
        using var activity = StartActivity("disrupting some container", ActivityKind.Internal);

        var containers = await dockerClient.Containers.ListContainersAsync(
            new ContainersListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["status"] = new Dictionary<string, bool> { ["running"] = true },
                    ["label"] = new Dictionary<string, bool> { ["chaos.disruption.target=true"] = true }
                }
            }, ct);

        var containerToDisrupt = containers.OrderBy(_ => Guid.NewGuid()).FirstOrDefault();

        if (containerToDisrupt is null)
        {
            logger.LogInformation("No containers found to disrupt");
            return;
        }

        _ = containerToDisrupt
            .Labels
            .TryGetValue("chaos.disruption.type", out var disruptionType);

        switch (disruptionType)
        {
            case "abrupt-kill":
                await KillContainerAsync(dockerClient, containerToDisrupt, ct);
                break;
            case "graceful-stop":
                await StopContainerAsync(dockerClient, containerToDisrupt, ct);
                break;
            default:
                logger.LogWarning(
                    "Unknown disruption type {DisruptionType} for container {ContainerName}",
                    disruptionType,
                    containerToDisrupt.Name());
                activity?.AddEvent(new(
                    "unknown disruption type",
                    tags: new([
                        new("disruption_type", disruptionType),
                        new("container_name", containerToDisrupt.Name())
                    ])));
                break;
        }
    }

    private async Task KillContainerAsync(
        DockerClient dockerClient,
        ContainerListResponse container,
        CancellationToken ct)
    {
        var disruptionDuration = CalculateDisruptionDuration();
        var containerName = container.Name();
        using var activity = StartActivity(
            "abruptly killing container",
            ActivityKind.Internal, tags:
            [
                new("container_name", containerName),
                new("disruption_duration", disruptionDuration.ToString())
            ]);
        logger.LogInformation(
            "Abruptly killing container {ContainerName} for {DisruptionDuration}",
            containerName,
            disruptionDuration);
        await dockerClient.Containers.StopContainerAsync(container.ID, new(), ct);
        await Task.Delay(disruptionDuration, ct);
        logger.LogInformation("Restarting container {ContainerName}", containerName);
        await dockerClient.Containers.StartContainerAsync(container.ID, new(), ct);
    }

    private async Task StopContainerAsync(
        DockerClient dockerClient,
        ContainerListResponse container,
        CancellationToken ct)
    {
        var disruptionDuration = CalculateDisruptionDuration();
        var containerName = container.Name();
        using var activity = StartActivity(
            "gracefully stopping container",
            ActivityKind.Internal, tags:
            [
                new("container_name", containerName),
                new("disruption_duration", disruptionDuration.ToString())
            ]);
        logger.LogInformation(
            "Gracefully stopping container {ContainerName} for {DisruptionDuration}",
            containerName,
            disruptionDuration);
        await dockerClient.Containers.StopContainerAsync(container.ID, new(), ct);
        await Task.Delay(disruptionDuration, ct);
        logger.LogInformation("Restarting container {ContainerName}", containerName);
        await dockerClient.Containers.StartContainerAsync(container.ID, new(), ct);
    }

    private TimeSpan CalculateDisruptionDuration()
        => TimeSpan.FromMilliseconds(
            Random.Shared.NextInt64(
                0,
                (long)settings.Value.MaxDisruptionDuration.TotalMilliseconds));
}

file static class DockerExtensions
{
    public static string Name(this ContainerListResponse container)
        => container.Names.FirstOrDefault()?.Trim('/') ?? container.ID;
}