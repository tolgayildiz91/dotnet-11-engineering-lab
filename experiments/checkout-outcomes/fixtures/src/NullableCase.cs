using Checkout.Model;

Console.WriteLine(NullableConsumer.Describe(new OrderCreated("ord-1", "SKU-1", 1, 12.50m)));

public union MaybeDeclined(OrderCreated, PaymentDeclined?);

public static class NullableConsumer
{
    // Both case arms present, null arm omitted.
    public static string Describe(MaybeDeclined outcome) => outcome switch
    {
        OrderCreated => "created",
        PaymentDeclined => "declined",
    };
}
