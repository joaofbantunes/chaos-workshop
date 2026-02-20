using ChaosWorkshop.MenuService.Features.Shared.Http;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace ChaosWorkshop.MenuService.Features.Menu;

public sealed class GetProductByIdEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/products/{id}", Handle);

    private readonly record struct Request
    {
        [FromRoute] public required string Id { get; init; }
    }

    private sealed record Response
    {
        public required string Id { get; init; }
        public required decimal Price { get; init; }
    }

    private static async Task<IResult> Handle(
        [AsParameters] Request request,
        NpgsqlDataSource dataSource,
        CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var item = await connection.QuerySingleOrDefaultAsync<Response?>(
            new CommandDefinition(
                // lang=postgresql
                """
                SELECT id, price
                FROM public.products
                WHERE id = @Id
                """,
                parameters: new { request.Id },
                cancellationToken: ct));

        return item is not null ? Results.Ok(item) : Results.NotFound();
    }
}