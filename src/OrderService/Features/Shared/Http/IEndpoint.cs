using System.Reflection;

namespace ChaosWorkshop.OrderService.Features.Shared.Http;

public interface IEndpoint
{
    static abstract void Map(IEndpointRouteBuilder endpoints);
}

public static class EndpointExtensions
{
    public static void MapAppEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var endpointTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsAssignableTo(typeof(IEndpoint)) && t is { IsAbstract: false, IsInterface: false });

        foreach (var endpointType in endpointTypes)
        {
            var map = endpointType.GetMethod(nameof(IEndpoint.Map), BindingFlags.Public | BindingFlags.Static)!;
            map.Invoke(null, [endpoints]);
        }
    }
}