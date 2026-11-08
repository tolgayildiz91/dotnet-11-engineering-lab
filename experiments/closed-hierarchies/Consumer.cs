#nullable enable
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ClosedPayments;

namespace PaymentConsumer;

public static class Program
{
#if NULLABLE
    public static string Describe(PaymentOutcome? outcome) => outcome switch
#else
    public static string Describe(PaymentOutcome outcome) => outcome switch
#endif
    {
#if HANDLE_NULL
        null => "absent",
#endif
        Approved a => "approved:" + a.Receipt,
        Declined d => "declined:" + d.Code,
#if INTERMEDIATE
        ManualReview m => "manual:" + m.Reason,
#if !MISSING
        RiskReview r => "risk:" + r.Reason,
#endif
#elif !MISSING
        ReviewRequired r => "review:" + r.Reason,
#endif
#if FALLBACK
        _ => "other",
#endif
    };

#if FALLBACK
    private static readonly bool HasFallback = true;
#else
    private static readonly bool HasFallback = false;
#endif
#if HANDLE_NULL
    private static readonly bool HandlesNull = true;
#else
    private static readonly bool HandlesNull = false;
#endif
#if MISSING
    private static readonly bool HasLastArm = false;
#else
    private static readonly bool HasLastArm = true;
#endif

    private static readonly string Unmatched = "throw:" + typeof(SwitchExpressionException).FullName;
    private static int failures;

    private static string Unhandled() => HasFallback ? "other" : Unmatched;

    private static string Expect(bool handled, string value) => handled ? value : Unhandled();

    private static string Run(Func<string> action)
    {
        try { return action(); }
        catch (Exception ex) { return "throw:" + ex.GetType().FullName; }
    }

    private static void Check(string label, Func<string> action, string expected)
    {
        string actual = Run(action);
        bool ok = actual == expected;
        if (!ok) failures++;
        Console.WriteLine((ok ? "PASS " : "FAIL ") + label + ": actual=" + actual + " expected=" + expected);
    }

    public static int Main()
    {
        Console.WriteLine("runtime: " + RuntimeInformation.FrameworkDescription + " (" + Environment.Version + ")");

        Check("approved", () => Describe(new Approved("R-1")), "approved:R-1");
        Check("declined", () => Describe(new Declined("D-2")), "declined:D-2");
#if INTERMEDIATE
        Check("manual", () => Describe(new ManualReview("M-3")), "manual:M-3");
        Check("risk", () => Describe(new RiskReview("K-4")), Expect(HasLastArm, "risk:K-4"));
#else
        Check("review", () => Describe(new ReviewRequired("V-3")), Expect(HasLastArm, "review:V-3"));
#endif

        Check("forced-null", () => Describe(null!), HandlesNull ? "absent" : Unhandled());

#if INDIRECT
#if INTERMEDIATE
        Check("foreign-indirect", () => Describe(new ForeignPayments.ForeignReview()), Unhandled());
#else
        Check("foreign-indirect", () => Describe(new ForeignPayments.ForeignReview()), Expect(HasLastArm, "review:synthetic"));
#endif
#endif

        var pending = typeof(PaymentOutcome).Assembly.GetType("ClosedPayments.Pending");
        Console.WriteLine("info model-has-Pending: " + (pending is not null) + " (not exercised; no exhaustiveness claim)");

        Console.WriteLine(failures == 0 ? "RESULT PASS" : "RESULT FAIL failures=" + failures);
        return failures == 0 ? 0 : 1;
    }
}
