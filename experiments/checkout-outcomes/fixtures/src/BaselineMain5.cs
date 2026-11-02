using Checkout.Baseline;
using Checkout.Model;

Console.WriteLine(BaselineConsumer.Describe(Result<OrderCreated, CheckoutError>.Fail(new FraudReviewRequired("fr-1"))));

public sealed record FraudReviewRequired(string CaseId) : CheckoutError;
