public enum OutcomeKind { Invalid, Accepted, Rejected }
public readonly record struct Result(OutcomeKind Kind, int Value);
public static class Handler
{
    public static int Handle(Result value) => value.Kind switch
    {
        OutcomeKind.Invalid => 0,
        OutcomeKind.Accepted => value.Value,
        OutcomeKind.Rejected => -value.Value
    };
}
