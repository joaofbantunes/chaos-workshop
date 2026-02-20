namespace ChaosWorkshop.Client;

public static class TestBatteryDefinition
{
    public static IEnumerable<TestDefinition> Get(TestBattery battery) => battery switch
    {
        TestBattery.Single => Tests[..1],
        TestBattery.Half => Tests[..(Tests.Length / 2)],
        TestBattery.Complete => Tests,
        _ => throw new ArgumentOutOfRangeException(nameof(battery), battery, null)
    };

    private static readonly Guid Customer1 = Guid.Parse("d3932515-c8bf-45e7-af54-92cd0c73a9e2");
    private static readonly Guid Customer2 = Guid.Parse("4e957adb-6957-4976-83ca-e2cb91a97519");
    private static readonly Guid Customer3 = Guid.Parse("dc609502-3f15-45e9-9c5f-4a4df4cc0550");

    private static readonly TestDefinition[] Tests =
    [
        new()
        {
            Description = "Customer 1 places first order: 2 burgers and 1 fries, no discount",
            PlaceOrderRequest = new()
            {
                CustomerId = Customer1,
                Items =
                [
                    new() { Id = "burger", Quantity = 2 },
                    new() { Id = "fries", Quantity = 1 }
                ]
            },
            ExpectedPlacedOrderDetails = new()
            {
                DiscountPercentage = 0.00m,
                Amount = 25.00m
            },
            ExpectedCustomerOrdersCount = 1,
            GetCustomerDiscountRequest = new()
            {
                CustomerId = Customer1
            },
            ExpectedGetCustomerDiscountResponse = new()
            {
                DiscountPercentage = 0.01m
            }
        },
        new()
        {
            Description = "Customer 1 places second order: 1 coke and 2 fries, 1% discount",
            PlaceOrderRequest = new()
            {
                CustomerId = Customer1,
                Items =
                [
                    new() { Id = "fries", Quantity = 4 }
                ]
            },
            ExpectedPlacedOrderDetails = new()
            {
                DiscountPercentage = 0.01m,
                Amount = 19.80m
            },
            ExpectedCustomerOrdersCount = 2,
            GetCustomerDiscountRequest = new()
            {
                CustomerId = Customer1
            },
            ExpectedGetCustomerDiscountResponse = new()
            {
                DiscountPercentage = 0.02m
            }
        },
        new()
        {
            Description = "Customer 2 places first order: 2 cokes and 1 fries, no discount",
            PlaceOrderRequest = new()
            {
                CustomerId = Customer2,
                Items =
                [
                    new() { Id = "coke", Quantity = 2 },
                    new() { Id = "fries", Quantity = 1 }
                ]
            },
            ExpectedPlacedOrderDetails = new()
            {
                DiscountPercentage = 0.00m,
                Amount = 10.00m
            },
            ExpectedCustomerOrdersCount = 1,
            GetCustomerDiscountRequest = new()
            {
                CustomerId = Customer2
            },
            ExpectedGetCustomerDiscountResponse = new()
            {
                DiscountPercentage = 0.01m
            }
        },
        new()
        {
            Description = "Customer 2 places second order: 2 burgers and 2 fries, 1% discount",
            PlaceOrderRequest = new()
            {
                CustomerId = Customer2,
                Items =
                [
                    new() { Id = "burger", Quantity = 2 },
                    new() { Id = "fries", Quantity = 2 }
                ]
            },
            ExpectedPlacedOrderDetails = new()
            {
                DiscountPercentage = 0.01m,
                Amount = 29.70m
            },
            ExpectedCustomerOrdersCount = 2,
            GetCustomerDiscountRequest = new()
            {
                CustomerId = Customer2
            },
            ExpectedGetCustomerDiscountResponse = new()
            {
                DiscountPercentage = 0.02m
            }
        },
        new()
        {
            Description =  "Customer 2 places third order: 2 fries, 2% discount",
            PlaceOrderRequest = new()
            {
                CustomerId = Customer2,
                Items =
                [
                    new() { Id = "fries", Quantity = 2 }
                ]
            },
            ExpectedPlacedOrderDetails = new()
            {
                DiscountPercentage = 0.02m,
                Amount = 9.80m
            },
            ExpectedCustomerOrdersCount = 3,
            GetCustomerDiscountRequest = new()
            {
                CustomerId = Customer2
            },
            ExpectedGetCustomerDiscountResponse = new()
            {
                DiscountPercentage = 0.03m
            }
        },
        new()
        {
            Description = "Customer 3 places first order: 2 fries, no discount",
            PlaceOrderRequest = new()
            {
                CustomerId = Customer3,
                Items =
                [
                    new() { Id = "fries", Quantity = 2 }
                ]
            },
            ExpectedPlacedOrderDetails = new()
            {
                DiscountPercentage = 0.00m,
                Amount = 10.00m
            },
            ExpectedCustomerOrdersCount = 1,
            GetCustomerDiscountRequest = new()
            {
                CustomerId = Customer3
            },
            ExpectedGetCustomerDiscountResponse = new()
            {
                DiscountPercentage = 0.01m
            }
        },
    ];
}

public sealed record TestDefinition
{
    public required string Description { get; init; }
    public required PlaceOrderRequest PlaceOrderRequest { get; init; }
    public required ExpectedPlacedOrderDetails ExpectedPlacedOrderDetails { get; init; }
    public required int ExpectedCustomerOrdersCount { get; init; }
    public required GetCustomerDiscountRequest GetCustomerDiscountRequest { get; init; }
    public required GetCustomerDiscountResponse ExpectedGetCustomerDiscountResponse { get; init; }
}

public sealed record ExpectedPlacedOrderDetails
{
    public required decimal DiscountPercentage { get; init; }
    public required decimal Amount { get; init; }
}