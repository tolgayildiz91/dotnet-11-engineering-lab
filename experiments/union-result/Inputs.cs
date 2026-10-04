using System.Security.Cryptography;

namespace UnionResultLab;
public sealed class Inputs
{
    public const int Count = 1024;
    public bool[] Success { get; } = new bool[Count];
    public int[] Values { get; } = new int[Count];
    public ReferenceOutcome[] ReferenceUnions { get; }
    public ReferenceResult[] ReferenceResults { get; }
    public ValueOutcome[] ValueUnions { get; }
    public ValueResult[] ValueResults { get; }
    public string SequenceHash { get; }
    public Inputs(int successPercent, string? selectedMethod = null)
    {
        if (successPercent is not (50 or 95)) throw new ArgumentOutOfRangeException(nameof(successPercent));
        Array.Fill(Success, true, 0, Count * successPercent / 100);
        uint state = 730031;
        for (int i = Count - 1; i > 0; i--)
        {
            state = unchecked(1664525 * state + 1013904223);
            int j = (int)(((ulong)state * (uint)(i + 1)) >> 32);
            (Success[i], Success[j]) = (Success[j], Success[i]);
        }
        SequenceHash = Convert.ToHexString(SHA256.HashData(Success.Select(x => x ? (byte)1 : (byte)0).ToArray()));
        bool all=selectedMethod is null;
        ReferenceUnions=all || selectedMethod=="ReferenceUnionHandle" ? new ReferenceOutcome[Count] : [];
        ReferenceResults=all || selectedMethod=="ReferenceResultHandle" ? new ReferenceResult[Count] : [];
        ValueUnions=all || selectedMethod=="ValueUnionHandle" ? new ValueOutcome[Count] : [];
        ValueResults=all || selectedMethod=="ValueResultHandle" ? new ValueResult[Count] : [];
        for (int i = 0; i < Count; i++)
        {
            int value = Values[i] = 1000 + i * 7;
            // Identical reference instances on both sides in handling-only cases.
            if (Success[i] && (ReferenceUnions.Length>0 || ReferenceResults.Length>0))
            {
                var reference = new AcceptedRef(value);
                if(ReferenceUnions.Length>0) ReferenceUnions[i] = reference;
                if(ReferenceResults.Length>0) ReferenceResults[i] = ReferenceResult.Success(reference);
            }
            else if(!Success[i] && (ReferenceUnions.Length>0 || ReferenceResults.Length>0))
            {
                var reference = new RejectedRef(value);
                if(ReferenceUnions.Length>0) ReferenceUnions[i] = reference;
                if(ReferenceResults.Length>0) ReferenceResults[i] = ReferenceResult.Failure(reference);
            }
            if(ValueUnions.Length>0) ValueUnions[i]=Success[i] ? new AcceptedValue(value) : new RejectedValue(value);
            if(ValueResults.Length>0) ValueResults[i]=Success[i] ? ValueResult.Success(new(value)) : ValueResult.Failure(new(value));
        }
    }
}
