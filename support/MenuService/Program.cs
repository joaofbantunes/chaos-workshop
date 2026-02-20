using ChaosWorkshop.MenuService.Features.Shared.Data;
using ChaosWorkshop.MenuService.Features.Shared.Http;
using ChaosWorkshop.MenuService.Features.Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConfiguredOpenTelemetry();
builder.Services.AddConfiguredOpenTelemetry();

builder.Services.AddNpgsqlDataSource(builder.Configuration.GetConnectionString("Default")!);
builder.Services.AddHostedService<MigrationsRunner>();
builder.Services.AddHostedService<SeedRunner>();

var app = builder.Build();

app.MapAppEndpoints();

app.Run();
