using Checkout.Model;

public sealed record FraudReviewRequired(string CaseId);

public union CheckoutOutcome(OrderCreated, ValidationFailed, InventoryUnavailable, PaymentDeclined, FraudReviewRequired);
