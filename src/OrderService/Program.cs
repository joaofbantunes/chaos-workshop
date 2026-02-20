using ChaosWorkshop.OrderService.Features.Shared.ApiClients;
using ChaosWorkshop.OrderService.Features.Shared.Data;
using ChaosWorkshop.OrderService.Features.Shared.Http;
using ChaosWorkshop.OrderService.Features.Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConfiguredOpenTelemetry();
builder.Services.AddConfiguredOpenTelemetry();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddNpgsqlDataSource(builder.Configuration.GetConnectionString("Default")!);
builder.Services.AddHostedService<MigrationsRunner>();
builder.Services.AddHttpClient<MenuClient>(o =>
    o.BaseAddress = new Uri(builder.Configuration.GetValue<string>("ApiClients:Menu:BaseAddress")!));
builder.Services.AddHttpClient<LoyaltyClient>(o =>
    o.BaseAddress = new Uri(builder.Configuration.GetValue<string>("ApiClients:Loyalty:BaseAddress")!));

var app = builder.Build();

app.MapAppEndpoints();

app.Run();