using Checkout.Baseline;
using Checkout.Model;

var request = new CheckoutRequest("SKU-1", 2, false);
Console.WriteLine(BaselineConsumer.Describe(Rules.Create(request)));
Console.WriteLine(BaselineConsumer.Describe(Rules.Validate(request with { Quantity = 0 })!));
Console.WriteLine(BaselineConsumer.Describe(Rules.Reserve(request with { Quantity = 9 })!));
Console.WriteLine(BaselineConsumer.Describe(Rules.Charge(request with { DeclinePayment = true })!));
