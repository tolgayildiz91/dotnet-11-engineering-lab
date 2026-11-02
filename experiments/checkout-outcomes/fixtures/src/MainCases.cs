using Checkout.Model;

var request = new CheckoutRequest("SKU-1", 2, false);
Console.WriteLine(Consumer.Describe(Rules.Create(request)));
Console.WriteLine(Consumer.Describe(Rules.Validate(request with { Quantity = 0 })!));
Console.WriteLine(Consumer.Describe(Rules.Reserve(request with { Quantity = 9 })!));
Console.WriteLine(Consumer.Describe(Rules.Charge(request with { DeclinePayment = true })!));
