using ChaosWorkshop.OrderService.Features.Shared.ApiClients;
using ChaosWorkshop.OrderService.Features.Shared.Http;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace ChaosWorkshop.OrderService.Features.Orders;

// ReSharper disable once UnusedType.Global - discovered via reflection
public sealed class PlaceOrderEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/orders", Handle);

    private readonly record struct Request
    {
        [FromBody] public required Body Body { get; init; }
    }

    private readonly record struct Body
    {
        public required Guid CustomerId { get; init; }
        public required IReadOnlyList<OrderItem> Items { get; init; }
    }

    private readonly record struct OrderItem
    {
        public required string Id { get; init; }
        public required int Quantity { get; init; }
    }

    private readonly record struct Response
    {
        public required Guid OrderId { get; init; }
    }

    private static async Task<IResult> Handle(
        [AsParameters] Request request,
        NpgsqlDataSource dataSource,
        MenuClient menuClient,
        LoyaltyClient loyaltyClient,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        var itemPrices = new Dictionary<string, decimal>();
        foreach (var item in request.Body.Items)
        {
            var menuItem = await menuClient.GetItemByIdAsync(item.Id, ct);
            if (menuItem is null)
            {
                return Results.BadRequest(new
                {
                    Message = $"Item {item.Id} not found"
                });
            }

            itemPrices[item.Id] = menuItem.Price;
        }

        var discountPercentage = (await loyaltyClient.GetDiscountAsync(request.Body.CustomerId, ct)).DiscountPercentage;
        var orderId = Guid.CreateVersion7(timeProvider.GetUtcNow());
        var placedAt = timeProvider.GetUtcNow();
        var totalAmount = request.Body.Items.Sum(i => itemPrices[i.Id] * i.Quantity);
        var discountAmount = totalAmount * discountPercentage;
        var finalAmount = totalAmount - discountAmount;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(
            new CommandDefinition(
                // lang=postgresql
                """
                INSERT INTO public.orders (id, customer_id, placed_date, amount, discount_percentage)
                VALUES (@OrderId, @CustomerId, @PlacedAt, @FinalAmount, @DiscountPercentage);
                """,
                parameters: new
                {
                    OrderId = orderId,
                    CustomerId = request.Body.CustomerId,
                    PlacedAt = placedAt,
                    FinalAmount = finalAmount,
                    DiscountPercentage = discountPercentage
                },
                cancellationToken: ct));

        await connection.ExecuteAsync(
            new CommandDefinition(
                // lang=postgresql
                """
                INSERT INTO public.order_items (order_id, product_id, quantity, unit_price)
                VALUES (@OrderId, @ProductId, @Quantity, @UnitPrice);
                """,
                request.Body.Items.Select(i => new
                {
                    OrderId = orderId,
                    ProductId = i.Id,
                    Quantity = i.Quantity,
                    UnitPrice = itemPrices[i.Id]
                }),
                cancellationToken: ct));


        await loyaltyClient.CommunicateOrderAsync(orderId, request.Body.CustomerId, placedAt, ct);

        return Results.Ok(new Response
        {
            OrderId = orderId
        });
    }
}