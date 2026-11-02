using Checkout.Baseline;
using Checkout.Model;

public static class BaselineConsumer
{
    public static string Describe(Result<OrderCreated, CheckoutError> result) => result.Match(
        c => $"201 {c.OrderId}",
        error => error switch
        {
            ValidationFailed v => $"400 {v.Field}",
            InventoryUnavailable i => $"409 {i.Sku}",
            PaymentDeclined p => $"402 {p.ReasonCode}",
            _ => $"500 unmapped {error.GetType().Name}",
        });
}
