namespace ChaosWorkshop.HttpProxy.Features.Chaos;

internal static class HttpContextExtensions
{
    public static bool IsStandDownRequested(this HttpContext ctx)
        => ctx.Request.Headers.TryGetValue("Workshop-ChaosProxy-StandDown", out var headerValues)
           && bool.TryParse(headerValues.FirstOrDefault(), out var standDownRequested)
           && standDownRequested;
}