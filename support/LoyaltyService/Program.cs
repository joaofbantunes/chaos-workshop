using ChaosWorkshop.LoyaltyService.Features.Shared.Data;
using ChaosWorkshop.LoyaltyService.Features.Shared.Http;
using ChaosWorkshop.LoyaltyService.Features.Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConfiguredOpenTelemetry();
builder.Services.AddConfiguredOpenTelemetry();

builder.Services.AddNpgsqlDataSource(builder.Configuration.GetConnectionString("Default")!);
builder.Services.AddHostedService<MigrationsRunner>();

var app = builder.Build();

app.MapAppEndpoints();

app.Run();
