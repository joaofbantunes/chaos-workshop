using ChaosWorkshop.LoyaltyService.Features.Shared.Http;
using Dapper;
using Npgsql;

namespace ChaosWorkshop.LoyaltyService.Features.Assertions;

public sealed class ResetDbEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/assertions/reset-db", Handle);

    private static async Task<IResult> Handle(
        NpgsqlDataSource dataSource,
        CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        await connection.ExecuteAsync(
            new CommandDefinition(
                commandText:
                // lang=postgresql
                """
                TRUNCATE TABLE public.orders RESTART IDENTITY CASCADE;
                """,
                cancellationToken: ct));

        return Results.NoContent();
    }
}