using System;
public sealed record Accepted(int CourseId);
public sealed record Rejected(int ReasonCode);
public sealed record Deferred(int WaitSeconds);
public union Outcome(Accepted, Rejected, Deferred);
public static class Handler
{
    public static int Handle(Outcome value) => value switch
    {
        Accepted a => a.CourseId,
        Rejected r => -r.ReasonCode
    };
}
