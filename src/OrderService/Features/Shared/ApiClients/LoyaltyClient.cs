namespace ChaosWorkshop.OrderService.Features.Shared.ApiClients;

public sealed class LoyaltyClient(HttpClient client)
{
    public async Task<DiscountResponse> GetDiscountAsync(Guid customerId, CancellationToken ct)
        => await client.GetFromJsonAsync<DiscountResponse>($"customer-discounts/{customerId}", ct)
           ?? new DiscountResponse(0);

    public async Task CommunicateOrderAsync(
        Guid orderId,
        Guid customerId,
        DateTimeOffset placedAt,
        CancellationToken ct)
        => await client.PostAsJsonAsync(
            "orders",
            new
            {
                OrderId = orderId,
                CustomerId = customerId,
                PlacedAt = placedAt
            },
            ct);
}

public sealed record DiscountResponse(decimal DiscountPercentage);