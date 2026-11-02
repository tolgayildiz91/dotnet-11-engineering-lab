using System.Runtime.CompilerServices;

try
{
    Console.WriteLine(Consumer.Describe(new FraudReviewRequired("fr-1")));
    return 0;
}
catch (SwitchExpressionException e)
{
    Console.WriteLine($"FraudReviewRequired reached unchanged four-arm switch: {e.GetType().FullName}");
    return 42;
}
