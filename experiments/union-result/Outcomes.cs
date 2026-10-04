using System.Runtime.CompilerServices;

namespace UnionResultLab;

// Independently synthetic hobby climbing-course outcomes; no external records.
public sealed record AcceptedRef(int CourseId);
public sealed record RejectedRef(int ReasonCode);
public readonly record struct AcceptedValue(int CourseId);
public readonly record struct RejectedValue(int ReasonCode);
public union ReferenceOutcome(AcceptedRef, RejectedRef);
public union ValueOutcome(AcceptedValue, RejectedValue);
public union PrimitiveOutcome(int, string);

public enum OutcomeKind : byte { Invalid = 0, Accepted = 1, Rejected = 2 }

public readonly struct ReferenceResult
{
    public OutcomeKind Kind { get; }
    public AcceptedRef? Accepted { get; }
    public RejectedRef? Rejected { get; }
    private ReferenceResult(OutcomeKind kind, AcceptedRef? accepted, RejectedRef? rejected)
        => (Kind, Accepted, Rejected) = (kind, accepted, rejected);
    public static ReferenceResult Success(AcceptedRef value)
        => new(OutcomeKind.Accepted, value ?? throw new ArgumentNullException(nameof(value)), null);
    public static ReferenceResult Failure(RejectedRef value)
        => new(OutcomeKind.Rejected, null, value ?? throw new ArgumentNullException(nameof(value)));
}

public readonly struct ValueResult
{
    public OutcomeKind Kind { get; }
    public AcceptedValue Accepted { get; }
    public RejectedValue Rejected { get; }
    private ValueResult(OutcomeKind kind, AcceptedValue accepted, RejectedValue rejected)
        => (Kind, Accepted, Rejected) = (kind, accepted, rejected);
    public static ValueResult Success(AcceptedValue value) => new(OutcomeKind.Accepted, value, default);
    public static ValueResult Failure(RejectedValue value) => new(OutcomeKind.Rejected, default, value);
}

public static class Operations
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ReferenceOutcome CreateReferenceUnion(bool success, int value)
        => success ? new AcceptedRef(value) : new RejectedRef(value);
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ReferenceResult CreateReferenceResult(bool success, int value)
        => success ? ReferenceResult.Success(new(value)) : ReferenceResult.Failure(new(value));
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ValueOutcome CreateValueUnion(bool success, int value)
        => success ? new AcceptedValue(value) : new RejectedValue(value);
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ValueResult CreateValueResult(bool success, int value)
        => success ? ValueResult.Success(new(value)) : ValueResult.Failure(new(value));

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int HandleReferenceUnion(in ReferenceOutcome value) => value switch
    {
        AcceptedRef a => a.CourseId,
        RejectedRef r => -r.ReasonCode,
        _ => throw new InvalidOperationException("Outcome has no active case.")
    };
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int HandleReferenceResult(in ReferenceResult value) => value.Kind switch
    {
        OutcomeKind.Accepted => value.Accepted!.CourseId,
        OutcomeKind.Rejected => -value.Rejected!.ReasonCode,
        _ => throw new InvalidOperationException("Result has no active case.")
    };
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int HandleValueUnion(in ValueOutcome value) => value switch
    {
        AcceptedValue a => a.CourseId,
        RejectedValue r => -r.ReasonCode,
        _ => throw new InvalidOperationException("Outcome has no active case.")
    };
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int HandleValueResult(in ValueResult value) => value.Kind switch
    {
        OutcomeKind.Accepted => value.Accepted.CourseId,
        OutcomeKind.Rejected => -value.Rejected.ReasonCode,
        _ => throw new InvalidOperationException("Result has no active case.")
    };
}

public sealed record HttpOutcome(int StatusCode, string Kind, int Value);
public static class ResponseAdapter
{
    public static HttpOutcome From(in ReferenceOutcome value) => value switch
    {
        AcceptedRef a => new(200, "accepted", a.CourseId),
        RejectedRef r => new(422, "rejected", r.ReasonCode),
        _ => throw new InvalidOperationException("Empty outcome")
    };
    public static HttpOutcome From(in ReferenceResult value) => value.Kind switch
    {
        OutcomeKind.Accepted => new(200, "accepted", value.Accepted!.CourseId),
        OutcomeKind.Rejected => new(422, "rejected", value.Rejected!.ReasonCode),
        _ => throw new InvalidOperationException("Empty result")
    };
    public static HttpOutcome Validate(HttpOutcome? value) => value switch
    {
        { StatusCode: 200, Kind: "accepted" } => value,
        { StatusCode: 422, Kind: "rejected" } => value,
        _ => throw new ArgumentException("Unknown, missing or inconsistent wire outcome")
    };
}
