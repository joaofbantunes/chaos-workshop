using ChaosWorkshop.ContainerDisruptor.Features.Chaos;
using ChaosWorkshop.ContainerDisruptor.Features.Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConfiguredOpenTelemetry();
builder.Services.AddConfiguredOpenTelemetry();

builder.Services.Configure<ContainerDisruptorSettings>(builder.Configuration.GetSection("ContainerDisruptor"));

builder.Services.AddHostedService<ContainerDisruptorBackgroundService>();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();