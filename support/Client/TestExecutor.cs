using System.Diagnostics;
using Polly;
using Polly.Retry;
using Spectre.Console;

namespace ChaosWorkshop.Client;

public enum TestBattery
{
    Single,
    Half,
    Complete
}

public sealed class TestExecutor(OrderClient orderClient, LoyaltyClient loyaltyClient, TimeProvider timeProvider)
{
    public async Task ExecuteAsync(TestBattery battery, CancellationToken ct)
    {
        await ResetServicesAsync(ct);

        try
        {
            var allTestsPassed = await RunTestsAsync(TestBatteryDefinition.Get(battery), ct);
            AnsiConsole.MarkupLine(allTestsPassed
                ? ":trophy: [green]All tests passed[/]"
                : ":cross_mark: [red]Some tests failed[/]");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            AnsiConsole.MarkupLine("[yellow]:warning:[/] Test execution cancelled");
        }
        catch (HttpRequestException)
        {
            AnsiConsole.MarkupLine(":cross_mark: Too many failed requests, aborting test execution");
        }
        catch (Exception e)
        {
            AnsiConsole.MarkupLine($":cross_mark: Unexpected test execution error: {e.Message}");
        }
    }

    private async Task ResetServicesAsync(CancellationToken ct)
    {
        using var activity = StartActivity("reset services db", ActivityKind.Internal);

        await AnsiConsole.Status().StartAsync(
            "Resetting services before running tests...",
            async _ =>
            {
                await orderClient.ResetDbAsync(ct);
                await loyaltyClient.ResetDbAsync(ct);
            });
    }

    private async Task<bool> RunTestsAsync(IEnumerable<TestDefinition> tests, CancellationToken ct)
    {
        var allTestsPassed = true;
        foreach (var (test, index) in tests.Select((value, index) => (value, index)))
        {
            AnsiConsole.Write(new Rule($"Running test {index + 1}")
            {
                Justification = Justify.Left,
                Style = new Style(foreground: Color.Blue)
            });
            AnsiConsole.MarkupLine($":alien_monster: [blue]{test.Description}[/]");
            var startTimestamp = timeProvider.GetTimestamp();
            var testPassed = await RunTestAsync(test, index, ct);
            allTestsPassed = allTestsPassed && testPassed;
            if (testPassed)
            {
                AnsiConsole.Write(
                    new Rule(
                        $":check_mark_button: Test {index + 1} passed (took {GetElapsedTimeString(timeProvider, startTimestamp)})")
                    {
                        Justification = Justify.Left,
                        Style = new Style(foreground: Color.Green)
                    });
            }
            else
            {
                AnsiConsole.Write(
                    new Rule(
                        $":cross_mark: Test {index + 1} failed (took {GetElapsedTimeString(timeProvider, startTimestamp)})")
                    {
                        Justification = Justify.Left,
                        Style = new Style(foreground: Color.Red)
                    });
            }
        }

        return allTestsPassed;

        static string GetElapsedTimeString(TimeProvider timeProvider, long startTimestamp)
        {
            var elapsed = timeProvider.GetElapsedTime(startTimestamp);
            return elapsed switch
            {
                { Days: > 0 } => $"{elapsed.Days}d {elapsed.Hours}h {elapsed.Minutes}m {elapsed.Seconds}s",
                { Hours: > 0 } => $"{elapsed.Hours}h {elapsed.Minutes}m {elapsed.Seconds}s",
                { Minutes: > 0 } => $"{elapsed.Minutes}m {elapsed.Seconds}s",
                _ => $"{elapsed.Seconds}s"
            };
        }
    }

    private async Task<bool> RunTestAsync(TestDefinition test, int index, CancellationToken ct)
    {
        Guid orderId;
        using (var activity = StartActivity(
                   $"run test {index + 1}",
                   ActivityKind.Internal,
                   tags:
                   [
                       new("test.index", index)
                   ]))
        {
            if (activity is not null)
            {
                AnsiConsole.MarkupLine($"[orange1]The trace id for this run is: [bold]{activity.TraceId}[/][/]");
                var rawParameter = $$"""{"datasource":"tempo","queries":[{"query":"{{activity.TraceId}}"}]}""";
                AnsiConsole.MarkupLine(
                    $":link: [orange1]Link to Grafana > [/]http://localhost:3000/explore?left={Uri.EscapeDataString(rawParameter)}");
            }

            using (var _ = StartActivity("place order", ActivityKind.Internal))
            {
                try
                {
                    var result = await AnsiConsole.Status().StartAsync(
                        "Placing order...",
                        _ => orderClient.PlaceOrderAsync(test.PlaceOrderRequest, ct));
                    orderId = result.OrderId;
                }
                catch (HttpRequestException)
                {
                    AnsiConsole.MarkupLine(":cross_mark: Too many failed requests, aborting test execution");
                    return false;
                }
            }
        }

        using (var _ = StartActivity(
                   $"assert outcome for test {index + 1}",
                   ActivityKind.Internal, tags:
                   [
                       new("test.index", index)
                   ]))
        {
            if (orderId != Guid.Empty)
            {
                AnsiConsole.MarkupLine($":check_mark_button: Order placed with id: {orderId}");
            }
            else
            {
                AnsiConsole.MarkupLine(":cross_mark: Order placement failed");
                return false;
            }

            var orders = await AnsiConsole.Status().StartAsync(
                "Fetching orders...",
                _ =>
                {
                    using var orderListActivity = StartActivity("list orders", ActivityKind.Internal);
                    return orderClient.ListOrdersAsync(test.PlaceOrderRequest.CustomerId, ct);
                });

            var placedOrder = orders.FirstOrDefault(o => o.Id == orderId);

            if (placedOrder != null)
            {
                AnsiConsole.MarkupLine(":check_mark_button: Order found");
            }
            else
            {
                AnsiConsole.MarkupLine(":cross_mark: Order not found");
                return false;
            }

            var areOrderDetailsCorrect = true;

            if (placedOrder.DiscountPercentage == test.ExpectedPlacedOrderDetails.DiscountPercentage)
            {
                AnsiConsole.MarkupLine(
                    $":check_mark_button: Discount percentage is correct ({placedOrder.DiscountPercentage})");
            }
            else
            {
                areOrderDetailsCorrect = false;
                AnsiConsole.MarkupLine(
                    $":cross_mark: Discount percentage is incorrect (expected {test.ExpectedPlacedOrderDetails.DiscountPercentage}, got {placedOrder.DiscountPercentage})");
            }

            if (placedOrder.Amount == test.ExpectedPlacedOrderDetails.Amount)
            {
                AnsiConsole.MarkupLine($":check_mark_button: Amount is correct ({placedOrder.Amount})");
            }
            else
            {
                areOrderDetailsCorrect = false;
                AnsiConsole.MarkupLine(
                    $":cross_mark: Amount is incorrect (expected {test.ExpectedPlacedOrderDetails.Amount}, got {placedOrder.Amount})");
            }

            if (!areOrderDetailsCorrect) return false;

            if (orders.Count == test.ExpectedCustomerOrdersCount)
            {
                AnsiConsole.MarkupLine($":check_mark_button: Customer orders count is correct ({orders.Count})");
            }
            else
            {
                AnsiConsole.MarkupLine(
                    $":cross_mark: Customer orders count is incorrect (expected {test.ExpectedCustomerOrdersCount}, got {orders.Count})");
                return false;
            }

            // we'll retry a bit if the outcome isn't the expected one, in case the participant takes an approach that leverages eventual consistency
            var nextDiscountAssertResiliencePipeline = new ResiliencePipelineBuilder<GetCustomerDiscountResponse>()
                .AddRetry(new()
                    {
                        MaxRetryAttempts = 10,
                        Delay = TimeSpan.FromSeconds(3),
                        BackoffType = DelayBackoffType.Constant,
                        ShouldHandle = args =>
                            ValueTask.FromResult(
                                args.Outcome.Result!.DiscountPercentage
                                != test.ExpectedGetCustomerDiscountResponse.DiscountPercentage)
                    }
                ).Build();

            var nextLoyaltyDiscount = await AnsiConsole.Status().StartAsync(
                "Fetching loyalty discount...",
                async _ =>
                {
                    using var loyaltyDiscountActivity = StartActivity("get customer discount", ActivityKind.Internal);
                    return await nextDiscountAssertResiliencePipeline.ExecuteAsync(
                        async innerCt =>
                            await loyaltyClient.GetDiscountAsync(test.GetCustomerDiscountRequest.CustomerId, innerCt),
                        ct);
                });

            if (nextLoyaltyDiscount.DiscountPercentage == test.ExpectedGetCustomerDiscountResponse.DiscountPercentage)
            {
                AnsiConsole.MarkupLine(
                    $":check_mark_button: Loyalty discount percentage is correct ({nextLoyaltyDiscount.DiscountPercentage})");
            }
            else
            {
                AnsiConsole.MarkupLine(
                    $":cross_mark: Loyalty discount percentage is incorrect (expected {test.ExpectedGetCustomerDiscountResponse.DiscountPercentage}, got {nextLoyaltyDiscount.DiscountPercentage})");
                return false;
            }
        }

        return true;
    }
}