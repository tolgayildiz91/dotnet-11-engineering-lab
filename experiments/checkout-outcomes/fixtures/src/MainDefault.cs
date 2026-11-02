using System.Runtime.CompilerServices;

try
{
    Console.WriteLine(Consumer.Describe(default));
    return 0;
}
catch (SwitchExpressionException e)
{
    Console.WriteLine($"default union reached four-arm switch: {e.GetType().FullName}");
    return 42;
}
