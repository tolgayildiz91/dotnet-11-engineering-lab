using System;
public sealed record Accepted(int CourseId);
public sealed record Rejected(int ReasonCode);
public union Outcome(Accepted, Rejected);
public static class Conversion { public static Outcome Convert(DateTime value) => value; }
