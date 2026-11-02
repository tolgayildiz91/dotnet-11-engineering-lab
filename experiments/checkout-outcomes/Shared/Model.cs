// Shared synthetic checkout model. Compiled into every project, so it stays C# 12 compatible.
namespace Checkout.Model;

public sealed record CheckoutRequest(string Sku, int Quantity, bool DeclinePayment);

public sealed record OrderCreated(string OrderId, string Sku, int Quantity, decimal Total);

// Common base only so the baseline can use one TError. The native union lists the exact case types
// and does not need it. The hierarchy is open: anyone can derive another CheckoutError.
public abstract record CheckoutError;
public sealed record ValidationFailed(string Field, string Message) : CheckoutError;
public sealed record InventoryUnavailable(string Sku, int Requested, int Available) : CheckoutError;
public sealed record PaymentDeclined(string ReasonCode, decimal Amount) : CheckoutError;

// Deterministic rules, evaluated in order: validation, stock, payment, success.
// Each implementation composes these stages in its own idiom, so payloads are identical.
public static class Rules
{
    public const int Stock = 5;
    public const decimal UnitPrice = 12.50m;

    public static decimal Total(CheckoutRequest r) => r.Quantity * UnitPrice;

    public static ValidationFailed? Validate(CheckoutRequest r) =>
        r.Quantity <= 0 ? new ValidationFailed("quantity", "Quantity must be positive.")
        : string.IsNullOrWhiteSpace(r.Sku) ? new ValidationFailed("sku", "Sku is required.")
        : null;

    public static InventoryUnavailable? Reserve(CheckoutRequest r) =>
        r.Quantity > Stock ? new InventoryUnavailable(r.Sku, r.Quantity, Stock) : null;

    public static PaymentDeclined? Charge(CheckoutRequest r) =>
        r.DeclinePayment ? new PaymentDeclined("synthetic_decline", Total(r)) : null;

    public static OrderCreated Create(CheckoutRequest r) =>
        new($"ord-{r.Sku}-{r.Quantity}", r.Sku, r.Quantity, Total(r));
}
