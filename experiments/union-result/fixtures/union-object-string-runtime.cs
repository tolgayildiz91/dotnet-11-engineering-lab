using System;
using System.Runtime.InteropServices;
using System.Text.Json;
public union TextOutcome(object, string);
public static class Conversion
{
    public static TextOutcome Convert(string value) => value;
    public static string SelectCase(TextOutcome value) => value switch
    {
        string s => "string:" + s,
        object o => "object:" + o.GetType().Name
    };
}
public static class Program
{
    public static int Main()
    {
        TextOutcome fromString = Conversion.Convert("climbing");
        TextOutcome fromObject = new object();
        string stringSelection = Conversion.SelectCase(fromString);
        string objectSelection = Conversion.SelectCase(fromObject);
        // RC1 object pattern binds the boxed union wrapper; this is not a payload-type assertion.
        bool verified = stringSelection == "string:climbing" && objectSelection == "object:TextOutcome";
        Console.WriteLine(JsonSerializer.Serialize(new {
            runtime = RuntimeInformation.FrameworkDescription,
            loadedDirectory = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)),
            stringSelection, objectSelection, verified
        }));
        return verified ? 0 : 1;
    }
}
