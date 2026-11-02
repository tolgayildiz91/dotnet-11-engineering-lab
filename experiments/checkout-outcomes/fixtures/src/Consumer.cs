using Checkout.Model;

public static class Consumer
{
    public static string Describe(CheckoutOutcome outcome) => outcome switch
    {
        OrderCreated c => $"201 {c.OrderId}",
        ValidationFailed v => $"400 {v.Field}",
        InventoryUnavailable i => $"409 {i.Sku}",
        PaymentDeclined p => $"402 {p.ReasonCode}",
    };
}
