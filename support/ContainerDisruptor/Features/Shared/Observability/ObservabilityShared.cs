using System.Diagnostics;
using System.Reflection;

namespace ChaosWorkshop.ContainerDisruptor.Features.Shared.Observability;

internal static class ObservabilityShared
{
    internal static class Tracing
    {
        public static string ActivitySourceName { get; } = Assembly.GetExecutingAssembly().GetName().Name!;

        public static readonly ActivitySource ActivitySource = new(
            ActivitySourceName,
            Assembly.GetExecutingAssembly().GetName().Version!.ToString());

        public static Activity? StartActivity(
            string activityName,
            ActivityKind kind,
            IEnumerable<KeyValuePair<string, object?>>? tags = null,
            IEnumerable<ActivityLink>? links = null,
            ActivityContext? parentContext = null)
        {
            if (!ActivitySource.HasListeners())
            {
                return null;
            }

            return ActivitySource.StartActivity(
                name: activityName,
                kind: kind,
                parentContext ?? default,
                tags: tags,
                links: links);
        }
    }
}