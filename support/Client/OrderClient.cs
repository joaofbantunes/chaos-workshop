using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ChaosWorkshop.Client;

public sealed class OrderClient(HttpClient client)
{
    public async Task<PlaceOrderResponse> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct)
    {
        var idempotencyKey = Guid.NewGuid().ToString();
        var content = JsonContent.Create(
            request,
            OrderSerializerContext.Default.PlaceOrderRequest,
            mediaType: null);
        content.Headers.Add("Workshop-Idempotency-Key", idempotencyKey);
        
        var response = await client.PostAsync("orders", content, ct);
        
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PlaceOrderResponse>(
                   OrderSerializerContext.Default.PlaceOrderResponse,
                   ct)
               ?? throw new InvalidOperationException("Failed to deserialize PlaceOrderResponse");
    }

    public async Task<IReadOnlyList<OrderDto>> ListOrdersAsync(Guid? customerId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"orders?customerId={customerId}");
        // disable chaos for validation requests, to minimize noise
        request.Headers.Add("Workshop-ChaosProxy-StandDown", "true");
        var response = await client.SendAsync(request, ct);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<OrderDto>>(
                   OrderSerializerContext.Default.IReadOnlyListOrderDto,
                   ct)
               ?? throw new InvalidOperationException("Failed to deserialize List<OrderDto>");
    }

    public async Task ResetDbAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "assertions/reset-db");
        request.Headers.Add("Workshop-ChaosProxy-StandDown", "true");
        // disable chaos for validation requests, to minimize noise
        var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}

[JsonSerializable(typeof(PlaceOrderRequest))]
[JsonSerializable(typeof(PlaceOrderResponse))]
[JsonSerializable(typeof(OrderDto))]
[JsonSerializable(typeof(IReadOnlyList<OrderDto>))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class OrderSerializerContext : JsonSerializerContext;

public sealed record PlaceOrderRequest
{
    public required Guid CustomerId { get; init; }
    public required IReadOnlyList<OrderItem> Items { get; init; }

    public sealed record OrderItem
    {
        public required string Id { get; init; }
        public required int Quantity { get; init; }
    }
}

public sealed record PlaceOrderResponse
{
    public required Guid OrderId { get; init; }
}

public sealed record OrderDto
{
    public required Guid Id { get; init; }
    public required Guid CustomerId { get; init; }
    public required decimal DiscountPercentage { get; init; }
    public required decimal Amount { get; init; }
    public required DateTime PlacedDate { get; init; }
    public required IReadOnlyList<OrderItemDto> Items { get; init; }
}

public sealed record OrderItemDto
{
    public required string Id { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
}