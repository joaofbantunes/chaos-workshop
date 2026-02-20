using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ChaosWorkshop.Client;

public sealed class LoyaltyClient(HttpClient client)
{
    public async Task<GetCustomerDiscountResponse> GetDiscountAsync(Guid customerId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"customer-discounts/{customerId}");
        // disable chaos for validation requests, to minimize noise
        request.Headers.Add("Workshop-ChaosProxy-StandDown", "true");
        var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GetCustomerDiscountResponse>(
                   LoyaltySerializerContext.Default.GetCustomerDiscountResponse,
                   ct)
               ?? throw new InvalidOperationException("Failed to deserialize GetCustomerDiscountResponse");
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

[JsonSerializable(typeof(GetCustomerDiscountResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class LoyaltySerializerContext : JsonSerializerContext;

public sealed record GetCustomerDiscountRequest
{
    public required Guid CustomerId { get; init; }
}

public sealed record GetCustomerDiscountResponse
{
    public required decimal DiscountPercentage { get; init; }
}