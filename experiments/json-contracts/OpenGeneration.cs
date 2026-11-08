// NEGATIVE fixture (.NET 11 only, keep out of positive projects).
// An open (non-closed) base requests per-type closed-polymorphism inference and is a generated-context root.
// Expected: source generator diagnostic or generated-code/runtime failure; record the actual diagnostic id and text.
using System.Text.Json.Serialization;

namespace JsonContracts.Negative;

[JsonPolymorphic(InferClosedTypePolymorphism = true)]
public record OpenGeneratedBase;

public sealed record OpenGeneratedChild(int N) : OpenGeneratedBase;

[JsonSerializable(typeof(OpenGeneratedBase))]
internal partial class OpenGenerationContext : JsonSerializerContext { }
