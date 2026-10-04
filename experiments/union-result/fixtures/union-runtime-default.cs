using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using System;
public sealed record Accepted(int CourseId);
public sealed record Rejected(int ReasonCode);
public union Outcome(Accepted, Rejected);
public static class Handler
{
    public static int Handle(Outcome value) => value switch
    {
        Accepted a => a.CourseId,
        Rejected r => -r.ReasonCode
    };
}
public static class Program
{
    public static void Main()
    {
        var observations = new Dictionary<string, object?>();
        static object Observe(Func<Outcome> create)
        {
            try { Outcome value = create(); return new { returned = Handler.Handle(value), exception = (string?)null }; }
            catch (Exception ex) { return new { returned = (int?)null, exception = ex.GetType().FullName, message = ex.Message }; }
        }
        observations["accepted"] = Observe(() => new Accepted(7));
        observations["rejected"] = Observe(() => new Rejected(3));
        observations["default"] = Observe(() => default(Outcome));
        observations["null_accepted_reference"] = Observe(() => (Accepted)null!);
        observations["null_rejected_reference"] = Observe(() => (Rejected)null!);
        Console.WriteLine(JsonSerializer.Serialize(new { runtime = RuntimeInformation.FrameworkDescription, loadedDirectory = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)), unionIsValueType = typeof(Outcome).IsValueType, observations }));
    }
}
