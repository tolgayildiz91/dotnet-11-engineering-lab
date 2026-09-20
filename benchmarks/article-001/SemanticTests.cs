using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace EngineeringLab.Article001;

/// <summary>Untimed correctness checks against independent reference algorithms.</summary>
public static class SemanticTests
{
    public static object Run()
    {
        var data = new SyntheticData();
        var original = Fingerprints(data);
        Equal(256, data.ScanRecords.Length, "scan batch size");
        Equal(4096, data.Inventory.Count, "inventory size");
        Equal(1024, data.Requests.Length, "request batch size");
        Equal(1024, data.Payments.Length, "payment batch size");
        Equal(4096, data.WindReadings.Length, "telemetry batch size");
        Equal(256, data.Events.Length, "transformation batch size");
        Equal(2048, data.SequenceKeys.Length, "sort batch size");
        SameFingerprints(original, Fingerprints(new SyntheticData()), "deterministic generation");

        int hits = 0;
        foreach (string request in data.Requests)
        {
            foreach (string key in data.Inventory.Keys)
            {
                if (!string.Equals(key, request, StringComparison.Ordinal)) continue;
                Check(!ReferenceEquals(key, request), "equal-content request must be a distinct string");
                hits++;
                break;
            }
        }
        Equal(768, hits, "exact 75% inventory hit distribution");

        long scanChecksum = ReferenceParse(data.ScanRecords);
        long inventoryChecksum = ReferenceInventory(data.Inventory, data.Requests);
        long paymentChecksum = ReferencePayments(data.Payments);
        long telemetryChecksum = ReferenceTelemetry(data.WindReadings);
        int[] expectedOrder = ReferenceSort(data.SequenceKeys);
        Check(!data.SequenceKeys.SequenceEqual(expectedOrder), "sorting input must be unsorted");
        Check(data.SequenceKeys.Distinct().Count() < data.SequenceKeys.Length, "sorting input must contain duplicates");

        ShipmentCheckpoint[]? previousGraph = null;
        int[]? previousOrder = null;
        for (int invocation = 0; invocation < 3; invocation++)
        {
            Equal(scanChecksum, Workloads.ParseParcelScans(data.ScanRecords), "scan reference");
            Equal(inventoryChecksum, Workloads.ResolveInventory(data.Inventory, data.Requests), "inventory reference");
            Equal(paymentChecksum, Workloads.EvaluatePaymentRisk(data.Payments), "payment reference");
            Equal(telemetryChecksum, Workloads.AggregateWindTelemetry(data.WindReadings), "telemetry reference");
            ShipmentCheckpoint[] graph = Workloads.TransformCheckpoints(data.Events);
            VerifyGraph(data.Events, graph, previousGraph);
            int[] ordered = Workloads.SortScanSequence(data.SequenceKeys);
            Check(!ReferenceEquals(ordered, data.SequenceKeys), "sort must clone source");
            Check(!ReferenceEquals(ordered, previousOrder), "sort must allocate a fresh output");
            Check(ordered.SequenceEqual(expectedOrder), "independent insertion-sort reference");
            VerifyMultiset(data.SequenceKeys, ordered);
            previousGraph = graph;
            previousOrder = ordered;
            SameFingerprints(original, Fingerprints(data), "source integrity after repeated invocation");
        }

        VerifyParseEdges();
        VerifyInventoryEdges();
        int riskBoundaryCases = VerifyRiskEdges();
        VerifyTelemetryEdges();
        VerifyTransformationEdges();
        VerifySortEdges();
        int[] decisions = new int[3];
        foreach (PaymentAuthorization payment in data.Payments)
        {
            int expected = ReferenceRisk(payment);
            Equal(expected, Workloads.EvaluateRisk(payment), "individual dataset decision");
            decisions[expected]++;
        }
        Check(decisions.All(count => count > 0), "dataset exercises every risk outcome");

        return new
        {
            Status = "PASS",
            Seed = 12026,
            RepeatedInvocations = 3,
            RiskBoundaryCases = riskBoundaryCases,
            BatchSizes = new { ParseParcelScans = 256, InventoryEntries = 4096, ResolveInventory = 1024,
                EvaluatePaymentRisk = 1024, AggregateWindTelemetry = 4096, TransformCheckpoints = 256, SortScanSequence = 2048 },
            InventoryRequests = new { Hits = hits, Misses = data.Requests.Length - hits, DistinctEqualContentHitStrings = hits },
            RiskDistribution = new { Allow = decisions[0], Review = decisions[1], Decline = decisions[2] },
            DatasetFingerprints = original,
            WorkloadChecksums = new
            {
                ParseParcelScans = scanChecksum,
                ResolveInventory = inventoryChecksum,
                EvaluatePaymentRisk = paymentChecksum,
                AggregateWindTelemetry = telemetryChecksum,
                TransformCheckpoints = GraphFingerprint(previousGraph!),
                SortScanSequence = IntFingerprint(previousOrder!)
            }
        };
    }

    private static long ReferenceParse(string[] records)
    {
        long result = 0;
        foreach (string record in records)
        {
            string[] fields = record.Split('|');
            if (fields.Length != 3 || !fields[0].StartsWith("SYN-", StringComparison.Ordinal) || fields[0].Length <= 4)
                throw new FormatException("Invalid synthetic scan record.");
            try
            {
                int sequence = int.Parse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture);
                int weight = int.Parse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture);
                result = checked(result + sequence + weight);
            }
            catch (OverflowException error)
            {
                throw new FormatException("Numeric field exceeds Int32.", error);
            }
        }
        return result;
    }

    private static long ReferenceInventory(Dictionary<string, int> inventory, string[] requests)
    {
        long result = 0;
        foreach (string request in requests)
            foreach (KeyValuePair<string, int> entry in inventory)
                if (string.Equals(request, entry.Key, StringComparison.Ordinal))
                {
                    result = checked(result + entry.Value);
                    break;
                }
        return result;
    }

    private static int ReferenceRisk(PaymentAuthorization payment)
    {
        // Declarative ordered rules deliberately differ from the benchmark's control flow.
        (bool Applies, int Decision)[] rules =
        {
            (payment.BlockedDevice, 2),
            (payment.AmountCents >= 100000 && payment.Velocity >= 6, 2),
            (payment.CrossBorder && payment.AmountCents >= 50000, 1),
            (payment.DeviceAgeDays < 7 && payment.AmountCents >= 20000, 1),
            (payment.Velocity >= 4, 1),
            (true, 0)
        };
        return rules.First(rule => rule.Applies).Decision;
    }

    private static long ReferencePayments(PaymentAuthorization[] payments) =>
        payments.Select((payment, index) => checked((index + 1L) * (ReferenceRisk(payment) + 1))).Sum();

    private static long ReferenceTelemetry(int[] readings)
    {
        long sum = readings.Aggregate(0L, (total, value) => checked(total + value));
        long energy = readings.Aggregate(0L, (total, value) => checked(total + checked((long)value * value)));
        return checked(sum + energy);
    }

    private static int[] ReferenceSort(int[] values)
    {
        var result = new int[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            int at = i;
            while (at > 0 && result[at - 1] > values[i])
            {
                result[at] = result[at - 1];
                at--;
            }
            result[at] = values[i];
        }
        return result;
    }

    private static void VerifyParseEdges()
    {
        string[][] valid = { Array.Empty<string>(), new[] { "SYN-A|0|0" },
            new[] { "SYN-A|2147483647|2147483647", "SYN-B|001|002" } };
        foreach (string[] records in valid)
            Equal(ReferenceParse(records), Workloads.ParseParcelScans(records), "valid parse boundary");
        string[] invalid = { "", "SYN-A", "SYN-|1|2", "syn-A|1|2", "A|1|2", "SYN-A|1", "SYN-A|1|2|3",
            "SYN-A||2", "SYN-A|1|", "SYN-A|-1|2", "SYN-A|1|-2", "SYN-A|+1|2", "SYN-A|1|+2",
            "SYN-A| 1|2", "SYN-A|1 |2", "SYN-A|1| 2", "SYN-A|1|2 ", "SYN-A|1.0|2", "SYN-A|1|2,000",
            "SYN-A|2147483648|1", "SYN-A|1|2147483648", "SYN-A|x|2", "SYN-A|1|\u0662" };
        foreach (string record in invalid)
        {
            ThrowsFormat(() => ReferenceParse(new[] { record }), "invalid reference parse");
            ThrowsFormat(() => Workloads.ParseParcelScans(new[] { record }), "invalid workload parse");
        }
    }

    private static void VerifyInventoryEdges()
    {
        var inventory = new Dictionary<string, int>(StringComparer.Ordinal) { ["SYN-A"] = int.MaxValue, ["SYN-Z"] = 0 };
        string[] requests = { "SYN-A", "syn-a", "SYN-Z", "SYN-missing", "SYN-A" };
        Equal(4294967294L, Workloads.ResolveInventory(inventory, requests), "ordinal lookup, duplicates, zero stock, long sum");
        Equal(0L, Workloads.ResolveInventory(inventory, Array.Empty<string>()), "empty inventory requests");
        Equal(0L, Workloads.ResolveInventory(new Dictionary<string, int>(StringComparer.Ordinal), requests), "empty inventory");
    }

    private static int VerifyRiskEdges()
    {
        int count = 0;
        foreach (int amount in new[] { 0, 19999, 20000, 49999, 50000, 99999, 100000, int.MaxValue })
        foreach (int velocity in new[] { 0, 3, 4, 5, 6 })
        foreach (int age in new[] { 0, 6, 7 })
        foreach (bool crossBorder in new[] { false, true })
        foreach (bool blocked in new[] { false, true })
        {
            var payment = new PaymentAuthorization(amount, velocity, age, crossBorder, blocked);
            Equal(ReferenceRisk(payment), Workloads.EvaluateRisk(payment), "risk threshold and precedence grid");
            count++;
        }
        var explicitCases = new (PaymentAuthorization Payment, int Expected)[]
        {
            (new(0, 0, 7, false, false), 0), (new(0, 0, 7, false, true), 2),
            (new(19999, 0, 6, false, false), 0), (new(20000, 0, 6, false, false), 1),
            (new(20000, 0, 7, false, false), 0), (new(49999, 0, 7, true, false), 0),
            (new(50000, 0, 7, true, false), 1), (new(50000, 0, 7, false, false), 0),
            (new(0, 3, 7, false, false), 0), (new(0, 4, 7, false, false), 1),
            (new(99999, 6, 7, false, false), 1), (new(100000, 5, 7, false, false), 1),
            (new(100000, 6, 7, false, false), 2), (new(100000, 6, 0, true, false), 2),
            (new(50000, 4, 0, true, true), 2)
        };
        foreach (var test in explicitCases)
            Equal(test.Expected, Workloads.EvaluateRisk(test.Payment), "explicit risk rule boundary");
        Equal(0L, Workloads.EvaluatePaymentRisk(Array.Empty<PaymentAuthorization>()), "empty payment batch");
        var ordered = new[] { new PaymentAuthorization(0, 0, 7, false, false),
            new PaymentAuthorization(0, 0, 7, false, true), new PaymentAuthorization(0, 4, 7, false, false) };
        Equal(13L, Workloads.EvaluatePaymentRisk(ordered), "weighted payment decision checksum");
        return count + explicitCases.Length;
    }

    private static void VerifyTelemetryEdges()
    {
        int[][] cases = { Array.Empty<int>(), new[] { 0 }, new[] { -1, 0, 1 }, new[] { -50000, 50000 },
            new[] { int.MaxValue }, new[] { int.MinValue } };
        foreach (int[] values in cases)
            Equal(ReferenceTelemetry(values), Workloads.AggregateWindTelemetry(values), "signed telemetry and widened square");
    }

    private static void VerifyTransformationEdges()
    {
        Equal(0, Workloads.TransformCheckpoints(Array.Empty<ParcelScanEvent>()).Length, "empty transformation");
        var events = new[] { new ParcelScanEvent("SYN-edge", 0, int.MaxValue), new ParcelScanEvent("SYN-edge", int.MaxValue, 0) };
        ShipmentCheckpoint[] first = Workloads.TransformCheckpoints(events);
        VerifyGraph(events, first, null);
        VerifyGraph(events, Workloads.TransformCheckpoints(events), first);
    }

    private static void VerifyGraph(ParcelScanEvent[] source, ShipmentCheckpoint[] graph, ShipmentCheckpoint[]? previous)
    {
        Equal(source.Length, graph.Length, "transformed graph length");
        Check(!ReferenceEquals(graph, previous), "fresh transformed array");
        var identities = new HashSet<ShipmentCheckpoint>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < source.Length; i++)
        {
            Check(graph[i] is not null, "non-null transformed checkpoint");
            Equal(source[i].ParcelId, graph[i].ParcelId, "transformed ID");
            Equal(source[i].Sequence, graph[i].Sequence, "transformed sequence");
            Equal(source[i].WeightGrams, graph[i].WeightGrams, "transformed weight");
            Check(ReferenceEquals(source[i].ParcelId, graph[i].ParcelId), "transformation shares existing ID string");
            Check(identities.Add(graph[i]), "distinct checkpoint instance per event");
        }
        if (previous is not null)
            foreach (ShipmentCheckpoint item in previous)
                Check(!identities.Contains(item), "fresh checkpoint graph across invocations");
    }

    private static void VerifySortEdges()
    {
        int[][] cases = { Array.Empty<int>(), new[] { 1 }, new[] { 2, 2, 2 }, new[] { 1, 2, 3 },
            new[] { 3, 2, 1 }, new[] { int.MaxValue, 0, int.MinValue, 0, -1 } };
        foreach (int[] values in cases)
        {
            int[] snapshot = (int[])values.Clone();
            int[] expected = ReferenceSort(values);
            int[] result = Workloads.SortScanSequence(values);
            Check(result.SequenceEqual(expected), "sort edge ordering");
            Check(values.SequenceEqual(snapshot), "sort edge source integrity");
            Check(!ReferenceEquals(values, result), "sort edge clones input");
            VerifyMultiset(values, result);
        }
    }

    private static void VerifyMultiset(int[] source, int[] ordered)
    {
        var counts = new Dictionary<int, int>();
        foreach (int value in source) counts[value] = counts.GetValueOrDefault(value) + 1;
        foreach (int value in ordered)
        {
            Check(counts.TryGetValue(value, out int remaining) && remaining > 0, "sort output multiplicity");
            counts[value] = remaining - 1;
        }
        Check(counts.Values.All(count => count == 0), "sort preserves input multiset");
    }

    private static Dictionary<string, string> Fingerprints(SyntheticData data) => new()
    {
        ["ScanRecords"] = Hash(writer => { foreach (string value in data.ScanRecords) writer.String(value); }),
        ["Inventory"] = Hash(writer => { foreach (var pair in data.Inventory.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            { writer.String(pair.Key); writer.Number(pair.Value); } }),
        ["Requests"] = Hash(writer => { foreach (string value in data.Requests) writer.String(value); }),
        ["Payments"] = Hash(writer => { foreach (var value in data.Payments)
            { writer.Number(value.AmountCents); writer.Number(value.Velocity); writer.Number(value.DeviceAgeDays);
                writer.Number(value.CrossBorder ? 1 : 0); writer.Number(value.BlockedDevice ? 1 : 0); } }),
        ["WindReadings"] = IntFingerprint(data.WindReadings),
        ["Events"] = Hash(writer => { foreach (var value in data.Events)
            { writer.String(value.ParcelId); writer.Number(value.Sequence); writer.Number(value.WeightGrams); } }),
        ["SequenceKeys"] = IntFingerprint(data.SequenceKeys)
    };

    private static string GraphFingerprint(ShipmentCheckpoint[] graph) => Hash(writer =>
    {
        foreach (ShipmentCheckpoint value in graph)
        { writer.String(value.ParcelId); writer.Number(value.Sequence); writer.Number(value.WeightGrams); }
    });

    private static string IntFingerprint(int[] values) => Hash(writer => { foreach (int value in values) writer.Number(value); });

    private static string Hash(Action<CanonicalWriter> append)
    {
        var writer = new CanonicalWriter();
        append(writer);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(writer.ToString())));
    }

    private sealed class CanonicalWriter
    {
        private readonly StringBuilder text = new();
        // Type tags, UTF-16 lengths and invariant integers make framing and culture explicit.
        public void String(string value) => text.Append('s').Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append('\n');
        public void Number(int value) => text.Append('i').Append(value.ToString(CultureInfo.InvariantCulture)).Append('\n');
        public override string ToString() => text.ToString();
    }

    private static void SameFingerprints(Dictionary<string, string> expected, Dictionary<string, string> actual, string message)
    {
        Equal(expected.Count, actual.Count, message);
        foreach (var pair in expected) Equal(pair.Value, actual[pair.Key], message + ": " + pair.Key);
    }

    private static void ThrowsFormat(Action operation, string message)
    {
        try { operation(); }
        catch (FormatException) { return; }
        throw new InvalidOperationException("Semantic check failed: " + message + " did not throw FormatException.");
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Semantic check failed: {message}; expected {expected}, actual {actual}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Semantic check failed: " + message + ".");
    }
}
