using ChaosWorkshop.LoyaltyService.Features.Shared.Http;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace ChaosWorkshop.LoyaltyService.Features.Discounts;

public sealed class GetCustomerDiscountEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/customer-discounts/{customerId}", Handle);

    private readonly record struct Request
    {
        [FromRoute] public required Guid CustomerId { get; init; }
    }

    private static async Task<IResult> Handle(
        [AsParameters] Request request,
        NpgsqlDataSource dataSource,
        CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        
        // very naive discount calculation: 1% discount for every order placed, up to 10%
        
        var discount = await connection.QuerySingleAsync<decimal>(
            new CommandDefinition(
                // lang=postgresql
                """
                SELECT COUNT(*) AS discount
                FROM (
                    SELECT 1
                    FROM public.orders
                    WHERE customer_id = @CustomerId
                    LIMIT 10
                ) AS limited_orders
                """,
                parameters: new { request.CustomerId },
                cancellationToken: ct));

        return Results.Ok(new { DiscountPercentage = discount / 100m });
    }
}