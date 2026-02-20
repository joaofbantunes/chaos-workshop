using System.Diagnostics;
using ChaosWorkshop.HttpProxy.Features.Chaos;
using ChaosWorkshop.HttpProxy.Features.Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.docker.json", optional: true);

builder.Logging.AddConfiguredOpenTelemetry();
builder.Services.AddConfiguredOpenTelemetry();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransformFactory<ChaosTransformFactory>();

var app = builder.Build();

app.Use((ctx, next) =>
{
    Activity.Current?.AddTag("is_stand_down_requested", ctx.IsStandDownRequested());
    return next();
});
app.MapReverseProxy();

app.MapGet("/", () => "Hello World!");

app.Run();