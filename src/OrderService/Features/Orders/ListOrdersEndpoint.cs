using ChaosWorkshop.OrderService.Features.Shared.Http;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace ChaosWorkshop.OrderService.Features.Orders;

// ReSharper disable once UnusedType.Global - discovered via reflection
public sealed class ListOrdersEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/orders", Handle);

    private readonly record struct Request
    {
        [FromQuery] public required Guid? CustomerId { get; init; }
    }

    private readonly record struct Order
    {
        public required Guid Id { get; init; }
        public required Guid CustomerId { get; init; }
        public required decimal DiscountPercentage { get; init; }
        public required decimal Amount { get; init; }
        public required DateTime PlacedDate { get; init; }
        public required IReadOnlyList<OrderItem> Items { get; init; }
    }

    private readonly record struct OrderItem
    {
        public required string Id { get; init; }
        public required int Quantity { get; init; }
        public required decimal UnitPrice { get; init; }
    }

    private static async Task<IResult> Handle(
        [AsParameters] Request request,
        NpgsqlDataSource dataSource,
        CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        await using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(
                commandText:
                // lang=postgresql
                """
                SELECT
                    id,
                    customer_id AS customerId,
                    discount_percentage AS discountPercentage,
                    amount,
                    placed_date AS placedDate
                FROM public.orders
                WHERE customer_id = @CustomerId OR @CustomerId IS NULL
                ORDER BY placed_date DESC;

                SELECT
                    oi.order_id AS orderId,
                    oi.product_id AS productId,
                    oi.quantity,
                    oi.unit_price AS unitPrice
                FROM public.order_items oi
                INNER JOIN public.orders o ON oi.order_id = o.id
                WHERE o.customer_id = @CustomerId OR @CustomerId IS NULL;
                """,
                parameters: new { request.CustomerId },
                cancellationToken: ct));

        var orders = await multi.ReadAsync<OrderFromDb>();
        var items = (await multi.ReadAsync<OrderItemFromDb>())
            .GroupBy(item => item.OrderId)
            .ToDictionary(g => g.Key, g => g.ToArray());

        return Results.Ok(orders.Select(orderFromDb => new Order
        {
            Id = orderFromDb.Id,
            CustomerId = orderFromDb.CustomerId,
            DiscountPercentage = orderFromDb.DiscountPercentage,
            Amount = orderFromDb.Amount,
            PlacedDate = orderFromDb.PlacedDate,
            Items = items.TryGetValue(orderFromDb.Id, out var orderItems)
                ? orderItems.Select(itemFromDb => new OrderItem
                {
                    Id = itemFromDb.ProductId,
                    Quantity = itemFromDb.Quantity,
                    UnitPrice = itemFromDb.UnitPrice
                }).ToArray()
                : []
        }));
    }

    private readonly record struct OrderFromDb
    {
        public required Guid Id { get; init; }
        public required Guid CustomerId { get; init; }
        public required decimal DiscountPercentage { get; init; }
        public required decimal Amount { get; init; }
        public required DateTime PlacedDate { get; init; }
    }

    private readonly record struct OrderItemFromDb
    {
        public required string ProductId { get; init; }
        public required int Quantity { get; init; }
        public required Guid OrderId { get; init; }
        public required decimal UnitPrice { get; init; }
    }
}