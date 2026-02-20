using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using ChaosWorkshop.Client;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using Spectre.Console;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .ConfigureResource(ConfigureResource)
    .AddSource(ActivitySourceName)
    .AddHttpClientInstrumentation()
    .AddOtlpExporter()
    .Build();

var cts = new CancellationTokenSource();

try
{
    var services = new ServiceCollection();
    services.AddSingleton(TimeProvider.System);
    services.AddSingleton<TestExecutor>();
    var retryStrategyOptions = new RetryStrategyOptions<HttpResponseMessage>
    {
        ShouldHandle = args => args.Outcome switch
        {
            { Exception: HttpRequestException } => PredicateResult.True(),
            { Exception: TimeoutRejectedException } => PredicateResult.True(),
            { Result.StatusCode: HttpStatusCode.RequestTimeout } => PredicateResult.True(),
            { Result.StatusCode: >= HttpStatusCode.InternalServerError } => PredicateResult.True(),
            _ => PredicateResult.False()
        },
        OnRetry = args =>
        {
            Activity.Current?.AddEvent(new(
                "http retry",
                tags: new ActivityTagsCollection
                {
                    { "retry.attempt", args.AttemptNumber },
                    { "retry.delay", args.RetryDelay.TotalMilliseconds }
                }));
            return ValueTask.CompletedTask;
        },
        MaxRetryAttempts = 100,
        // constant and fast retry delay, so the things run faster in a workshop setting
        BackoffType = DelayBackoffType.Constant,
        Delay = TimeSpan.FromMilliseconds(250),
        MaxDelay = TimeSpan.FromMilliseconds(250)
    };
    services
        .AddHttpClient<OrderClient>(client =>
        {
            client.BaseAddress = new Uri(configuration.GetValue<string>("ApiClients:Order:BaseAddress")!);
            client.Timeout = Timeout.InfiniteTimeSpan; // Disable HttpClient timeout to let Polly handle it
        })
        .AddResilienceHandler(
            "retry-a-bunch",
            pipeline => pipeline.AddRetry(retryStrategyOptions).AddTimeout(TimeSpan.FromSeconds(5)));

    services
        .AddHttpClient<LoyaltyClient>(client =>
        {
            client.BaseAddress = new Uri(configuration.GetValue<string>("ApiClients:Loyalty:BaseAddress")!);
            client.Timeout = Timeout.InfiniteTimeSpan; // Disable HttpClient timeout to let Polly handle it
        })
        .AddResilienceHandler(
            "retry-a-bunch",
            pipeline => pipeline.AddRetry(retryStrategyOptions).AddTimeout(TimeSpan.FromSeconds(5)));

    await using var sp = services.BuildServiceProvider();

    if (!TryGetOptions(args, out var options))
    {
        return 1;
    }

    var selection = options.Battery ?? AnsiConsole.Prompt(
        new SelectionPrompt<TestBattery>()
            .Title("What test battery would you like to run?")
            .PageSize(5)
            .MoreChoicesText("[grey](Move up and down to reveal more tests)[/]")
            .AddChoices(
                TestBattery.Single,
                TestBattery.Half,
                TestBattery.Complete)
            .UseConverter(selection => selection switch
            {
                TestBattery.Single => "Single order",
                TestBattery.Half => "Half test battery",
                TestBattery.Complete => "Complete test battery",
                _ => throw new ArgumentOutOfRangeException()
            }));


    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true; // Prevent immediate process termination
        cts.Cancel();
        AnsiConsole.MarkupLine("[yellow]Cancellation requested[/]");
    };

    AnsiConsole.MarkupLineInterpolated($"[blue]Executing {selection} test battery...[/]");

    await sp.GetRequiredService<TestExecutor>().ExecuteAsync(selection, cts.Token);

    return 0;
}
catch (OperationCanceledException) when (cts.IsCancellationRequested)
{
    return 1;
}
finally
{
    tracerProvider.ForceFlush((int)TimeSpan.FromSeconds(5).TotalMilliseconds);
}

static void ConfigureResource(ResourceBuilder r) => r.AddService(
    serviceName: typeof(Program).Assembly.GetName().Name ??
                 throw new InvalidOperationException("Unable to determine assembly name"),
    serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
    serviceInstanceId: Environment.MachineName);

[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(CliOptions))]
static bool TryGetOptions(string[] args, [MaybeNullWhen(false)] out CliOptions options)
{
    // using commandlineparser because it seems spectre.console.cli doesn't support native aot
    var result = Parser.Default.ParseArguments<CliOptions>(args);
    options = result.Value;
    return !result.Errors.Any();
}


public sealed record CliOptions
{
    [Option('b', "battery", Required = false, HelpText = $"The test battery to run (can be '{nameof(TestBattery.Single)}', '{nameof(TestBattery.Half)}' or '{nameof(TestBattery.Complete)}'). If not specified, an interactive prompt will be shown.")]
    public TestBattery? Battery { get; init; }
}