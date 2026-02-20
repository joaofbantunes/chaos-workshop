namespace ChaosWorkshop.OrderService.Features.Shared.ApiClients;

public sealed class MenuClient(HttpClient client)
{
    public async Task<Product?> GetItemByIdAsync(string productId, CancellationToken ct)
        => await client.GetFromJsonAsync<Product>($"products/{Uri.EscapeDataString(productId)}", ct);
}

public sealed record Product(string Id, decimal Price);