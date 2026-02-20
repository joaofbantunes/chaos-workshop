using System.Diagnostics;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace ChaosWorkshop.HttpProxy.Features.Chaos;

public sealed class ChaosTransformFactory : ITransformFactory
{
    private enum TypeOfFailure
    {
        AbortRequest,
        ServerError
    }

    private const string BeforeRequestKey = "BeforeRequestFailurePercent";
    private const string AfterResponseKey = "AfterResponseFailurePercent";
    private const string LatencyBeforeRequestKey = "BeforeRequestMaxLatency";
    private const string LatencyAfterResponseKey = "AfterResponseMaxLatency";

    public bool Validate(TransformRouteValidationContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        var matched = false;

        var (beforeRequestMatched, _, beforeRequestError) = ParseFailurePercentBeforeRequest(transformValues);
        if (beforeRequestMatched)
        {
            matched = true;
            if (beforeRequestError is not null) context.Errors.Add(beforeRequestError);
        }

        var (afterResponseMatched, _, afterResponseError) = ParseFailurePercentAfterResponse(transformValues);
        if (afterResponseMatched)
        {
            matched = true;
            if (afterResponseError is not null) context.Errors.Add(afterResponseError);
        }

        var (latencyBeforeRequestMatched, _, latencyBeforeRequestError) = ParseLatencyBeforeRequest(transformValues);
        if (latencyBeforeRequestMatched)
        {
            matched = true;
            if (latencyBeforeRequestError is not null) context.Errors.Add(latencyBeforeRequestError);
        }

        var (latencyAfterResponseMatched, _, latencyAfterResponseError) = ParseLatencyAfterResponse(transformValues);
        if (latencyAfterResponseMatched)
        {
            matched = true;
            if (latencyAfterResponseError is not null) context.Errors.Add(latencyAfterResponseError);
        }

        return matched;
    }

    public bool Build(TransformBuilderContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        var failurePercentBeforeRequest = ParseFailurePercentBeforeRequest(transformValues);
        var failurePercentAfterResponse = ParseFailurePercentAfterResponse(transformValues);
        var latencyBeforeRequest = ParseLatencyBeforeRequest(transformValues);
        var latencyAfterResponse = ParseLatencyAfterResponse(transformValues);

        var errors = new[]
            {
                failurePercentBeforeRequest.Error,
                failurePercentAfterResponse.Error,
                latencyBeforeRequest.Error,
                latencyAfterResponse.Error
            }
            .Where(e => e != null)
            .Cast<Exception>()
            .ToArray();

        if (errors.Length != 0) throw new AggregateException(errors);

        if (latencyBeforeRequest.Matched)
        {
            context.AddRequestTransform(async ctx =>
            {
                if (!ctx.HttpContext.RequestAborted.IsCancellationRequested && !ctx.HttpContext.IsStandDownRequested())
                {
                    var delay = CalculateRandomDelay(latencyBeforeRequest.Value);
                    ObserveDelay("chaos - apply latency before request", delay);
                    await Task.Delay(delay, ctx.HttpContext.RequestAborted);
                }
            });
        }

        if (failurePercentBeforeRequest.Matched)
        {
            context.AddRequestTransform(ctx =>
            {
                if (!ctx.HttpContext.RequestAborted.IsCancellationRequested && !ctx.HttpContext.IsStandDownRequested())
                {
                    var (shouldFail, typeOfFailure) = CalculateFailure(failurePercentBeforeRequest.Value);

                    ObservePotentialFailure(
                        "chaos - apply failure before request",
                        shouldFail,
                        typeOfFailure);

                    InduceFailure(ctx.HttpContext, shouldFail, typeOfFailure);
                }

                return ValueTask.CompletedTask;
            });
        }

        if (failurePercentAfterResponse.Matched)
        {
            context.AddResponseTransform(ctx =>
            {
                if (!ctx.HttpContext.RequestAborted.IsCancellationRequested && !ctx.HttpContext.IsStandDownRequested())
                {
                    var (shouldFail, typeOfFailure) = CalculateFailure(failurePercentAfterResponse.Value);

                    ObservePotentialFailure(
                        "chaos - apply failure after response",
                        shouldFail,
                        typeOfFailure);

                    InduceFailure(ctx.HttpContext, shouldFail, typeOfFailure);
                }

                return ValueTask.CompletedTask;
            });
        }

        if (latencyAfterResponse.Matched)
        {
            context.AddResponseTransform(async ctx =>
            {
                if (!ctx.HttpContext.RequestAborted.IsCancellationRequested && !ctx.HttpContext.IsStandDownRequested())
                {
                    var delay = CalculateRandomDelay(latencyAfterResponse.Value);
                    ObserveDelay("chaos - apply latency after response", delay);
                    await Task.Delay(delay, ctx.HttpContext.RequestAborted);
                }
            });
        }

        return failurePercentBeforeRequest.Matched
               || failurePercentAfterResponse.Matched
               || latencyBeforeRequest.Matched
               || latencyAfterResponse.Matched;
    }

    private static ParseResult<short> ParseFailurePercentBeforeRequest(
        IReadOnlyDictionary<string, string> transformValues)
        => ParseFailurePercent(BeforeRequestKey, transformValues);

    private static ParseResult<short> ParseFailurePercentAfterResponse(
        IReadOnlyDictionary<string, string> transformValues)
        => ParseFailurePercent(AfterResponseKey, transformValues);

    private static ParseResult<short> ParseFailurePercent(
        string key,
        IReadOnlyDictionary<string, string> transformValues)
    {
        if (transformValues.TryGetValue(key, out var failurePercentStringValue))
        {
            if (!short.TryParse(failurePercentStringValue, out var failurePercent)
                || failurePercent < 0
                || failurePercent > 100)
            {
                return new ParseResult<short>(
                    true,
                    0,
                    new ArgumentException($"Invalid value for {key}: {failurePercentStringValue}"));
            }

            return new ParseResult<short>(true, failurePercent, null);
        }

        return new ParseResult<short>(false, 0, null);
    }

    private static ParseResult<TimeSpan> ParseLatencyBeforeRequest(IReadOnlyDictionary<string, string> transformValues)
        => ParseTimeSpan(LatencyBeforeRequestKey, transformValues);

    private static ParseResult<TimeSpan> ParseLatencyAfterResponse(IReadOnlyDictionary<string, string> transformValues)
        => ParseTimeSpan(LatencyAfterResponseKey, transformValues);

    private static ParseResult<TimeSpan> ParseTimeSpan(string key, IReadOnlyDictionary<string, string> transformValues)
    {
        if (transformValues.TryGetValue(key, out var timeSpanStringValue))
        {
            if (!TimeSpan.TryParse(timeSpanStringValue, out var timeSpan) || timeSpan < TimeSpan.Zero)
            {
                return new ParseResult<TimeSpan>(
                    true,
                    TimeSpan.Zero,
                    new ArgumentException($"Invalid value for {key}: {timeSpanStringValue}"));
            }

            return new ParseResult<TimeSpan>(true, timeSpan, null);
        }

        return new ParseResult<TimeSpan>(false, TimeSpan.Zero, null);
    }

    private static TimeSpan CalculateRandomDelay(TimeSpan maxDelay)
    {
        var delay = Random.Shared.NextInt64(0, (long)maxDelay.TotalMilliseconds + 1);
        return TimeSpan.FromMilliseconds(delay);
    }

    private static void ObserveDelay(string eventName, TimeSpan delay)
        => Activity.Current?.AddEvent(
            new(
                eventName,
                tags: new([new("delay", delay.ToString())])));

    private static (bool ShouldFail, TypeOfFailure TypeOfFailure) CalculateFailure(short failurePercent)
    {
        var shouldFail = Random.Shared.Next(0, 100 + 1) < failurePercent;
        var typeOfFailure = Random.Shared.Next(0, 2) == 0
            ? TypeOfFailure.AbortRequest
            : TypeOfFailure.ServerError;

        return (shouldFail, typeOfFailure);
    }

    private static void ObservePotentialFailure(
        string eventName,
        bool shouldFail,
        TypeOfFailure typeOfFailure)
        => Activity.Current?.AddEvent(
            new(
                eventName,
                tags: new(
                    shouldFail
                        ?
                        [
                            new("should_fail", shouldFail),
                            new("type_of_failure", typeOfFailure.ToString())
                        ]
                        : [new("should_fail", shouldFail)])));

    private static void InduceFailure(HttpContext ctx, bool shouldFail, TypeOfFailure typeOfFailure)
    {
        if (shouldFail)
        {
            switch (typeOfFailure)
            {
                case TypeOfFailure.AbortRequest:
                    ctx.Abort();
                    break;
                case TypeOfFailure.ServerError:
                    ctx.Response.StatusCode = 503;
                    break;
            }
        }
    }

    private readonly record struct ParseResult<T>(bool Matched, T Value, ArgumentException? Error);
}