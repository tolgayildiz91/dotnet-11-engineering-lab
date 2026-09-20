using System.Globalization;

namespace EngineeringLab.Article001;

public readonly record struct PaymentAuthorization(int AmountCents, int Velocity, int DeviceAgeDays, bool CrossBorder, bool BlockedDevice);
public readonly record struct ParcelScanEvent(string ParcelId, int Sequence, int WeightGrams);

public sealed class ShipmentCheckpoint(string parcelId, int sequence, int weightGrams)
{
    public string ParcelId { get; } = parcelId;
    public int Sequence { get; } = sequence;
    public int WeightGrams { get; } = weightGrams;
}

public static class Workloads
{
    public static long ParseParcelScans(string[] records)
    {
        long checksum = 0;
        foreach (string record in records)
        {
            ReadOnlySpan<char> span = record.AsSpan();
            int first = span.IndexOf('|');
            if (first <= 4 || !span[..first].StartsWith("SYN-", StringComparison.Ordinal))
                throw new FormatException("Invalid synthetic scan identifier.");
            ReadOnlySpan<char> remainder = span[(first + 1)..];
            int second = remainder.IndexOf('|');
            if (second < 1 ||
                !int.TryParse(remainder[..second], NumberStyles.None, CultureInfo.InvariantCulture, out int sequence) ||
                !int.TryParse(remainder[(second + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out int weight))
                throw new FormatException("Invalid scan numeric fields.");
            checksum += (long)sequence + weight;
        }
        return checksum;
    }

    public static long ResolveInventory(Dictionary<string, int> inventory, string[] requests)
    {
        long available = 0;
        foreach (string sku in requests)
            if (inventory.TryGetValue(sku, out int quantity)) available += quantity;
        return available;
    }

    // Fictional rules, chosen for branch coverage; not a real financial policy.
    public static int EvaluateRisk(PaymentAuthorization payment)
    {
        if (payment.BlockedDevice || (payment.AmountCents >= 100_000 && payment.Velocity >= 6)) return 2;
        if ((payment.CrossBorder && payment.AmountCents >= 50_000) ||
            (payment.DeviceAgeDays < 7 && payment.AmountCents >= 20_000) || payment.Velocity >= 4) return 1;
        return 0;
    }

    public static long EvaluatePaymentRisk(PaymentAuthorization[] payments)
    {
        long checksum = 0;
        for (int i = 0; i < payments.Length; i++) checksum += (i + 1L) * (EvaluateRisk(payments[i]) + 1);
        return checksum;
    }

    public static long AggregateWindTelemetry(int[] readings)
    {
        long sum = 0, energy = 0;
        foreach (int reading in readings)
        {
            sum += reading;
            energy += (long)reading * reading;
        }
        return sum + energy;
    }

    public static ShipmentCheckpoint[] TransformCheckpoints(ParcelScanEvent[] events)
    {
        var result = new ShipmentCheckpoint[events.Length];
        for (int i = 0; i < events.Length; i++)
        {
            ParcelScanEvent item = events[i];
            result[i] = new ShipmentCheckpoint(item.ParcelId, item.Sequence, item.WeightGrams);
        }
        return result;
    }

    public static int[] SortScanSequence(int[] sequence)
    {
        var result = (int[])sequence.Clone();
        Array.Sort(result);
        return result;
    }
}

public sealed class SyntheticData
{
    public string[] ScanRecords { get; } = new string[256];
    public Dictionary<string, int> Inventory { get; } = new(4096, StringComparer.Ordinal);
    public string[] Requests { get; } = new string[1024];
    public PaymentAuthorization[] Payments { get; } = new PaymentAuthorization[1024];
    public int[] WindReadings { get; } = new int[4096];
    public ParcelScanEvent[] Events { get; } = new ParcelScanEvent[256];
    public int[] SequenceKeys { get; } = new int[2048];

    public SyntheticData(uint seed = 12026)
    {
        uint state = seed;
        int Next(int maximum)
        {
            state = unchecked(state * 1664525u + 1013904223u);
            return (int)((state >> 8) % (uint)maximum);
        }
        for (int i = 0; i < Events.Length; i++)
        {
            string id = FormattableString.Invariant($"SYN-PARCEL-{i:D6}");
            Events[i] = new(id, Next(100_000), 1 + Next(30_000));
            ScanRecords[i] = FormattableString.Invariant($"{id}|{Events[i].Sequence}|{Events[i].WeightGrams}");
        }
        for (int i = 0; i < 4096; i++) Inventory.Add(FormattableString.Invariant($"SYN-SKU-{i:D6}"), Next(500));
        for (int i = 0; i < Requests.Length; i++)
        {
            // New strings have equal content on hits but are never dictionary-key references.
            int index = i % 4 == 0 ? 4096 + Next(4096) : Next(4096);
            Requests[i] = FormattableString.Invariant($"SYN-SKU-{index:D6}");
        }
        for (int i = 0; i < Payments.Length; i++)
            Payments[i] = new(1 + Next(150_000), Next(9), Next(60), Next(5) == 0, Next(25) == 0);
        for (int i = 0; i < WindReadings.Length; i++) WindReadings[i] = Next(12_001) - 6000;
        for (int i = 0; i < SequenceKeys.Length; i++) SequenceKeys[i] = Next(8192);
    }
}
