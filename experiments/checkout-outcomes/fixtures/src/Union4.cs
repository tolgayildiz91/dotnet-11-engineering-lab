using Checkout.Model;

public union CheckoutOutcome(OrderCreated, ValidationFailed, InventoryUnavailable, PaymentDeclined);
