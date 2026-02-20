using ChaosWorkshop.LoyaltyService.Features.Shared.Http;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace ChaosWorkshop.LoyaltyService.Features.Orders;

public sealed class CommunicateOrderEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/orders", Handle);

    private readonly record struct Request
    {
        [FromBody] public required Body Body { get; init; }
    }

    private readonly record struct Body
    {
        public required Guid OrderId { get; init; }
        public required Guid CustomerId { get; init; }
        public required DateTimeOffset PlacedAt { get; init; }
    }

    private static async Task<IResult> Handle(
        [AsParameters] Request request,
        NpgsqlDataSource dataSource,
        CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        await connection.ExecuteAsync(
            new CommandDefinition(
                // lang=postgresql
                """
                INSERT INTO public.orders (id, customer_id, placed_date)
                VALUES (@OrderId, @CustomerId, @PlacedAt)
                ON CONFLICT (id) DO NOTHING;
                """,
                parameters: new
                {
                    request.Body.OrderId,
                    request.Body.CustomerId,
                    request.Body.PlacedAt
                },
                cancellationToken: ct));

        return Results.NoContent();
    }
}