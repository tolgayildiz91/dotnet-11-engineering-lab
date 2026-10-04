using System;
public sealed record Accepted(int CourseId);
public sealed record Rejected(int ReasonCode);
public union Outcome(Accepted, Rejected);
public static class Handler { public static int Handle(Outcome value) => value switch { Accepted a => a.CourseId }; }
