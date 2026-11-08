# Observed contract results

SDK 11.0.100-rc.3.26522.148; compiler 5.12.0-1.26522.148.
Eight builds and four executable configurations. Three intentional build
failures: API8/API10 CS0117, open-inference source generation SYSLIB1243.

| Contract | .NET 8.0.33 | .NET 10.0.14 | .NET 11 RC3 |
| --- | --- | --- | --- |
| Ordinary SerializeAsync async sequence | AVAILABLE [1,2] | SAME | SAME |
| SerializeAsyncEnumerable API | NOT AVAILABLE CS0117 | NOT AVAILABLE CS0117 | AVAILABLE |
| Generated included private setter/field | InvalidOperationException | SAME | Reads/writes |
| Generated private JsonConstructor | NotSupportedException | SAME | Reads/writes |
| Generated omitted init Mode | null | SAME | synthetic-default |
| Reflection private contracts | Work | SAME | SAME |
| Reflection omitted init Mode | synthetic-default | SAME | SAME |
| Explicit init Mode | explicit both modes | SAME | SAME |

Older-target generators warn SYSLIB1038 (private include) and SYSLIB1222
(inaccessible constructor); builds succeed before runtime failures.

## .NET 11 only

- ApiValue(int,string,OrderCreated) writes the active JSON value without wrapper.
  Default union writes null; reading null produces Value=null in the nonnullable
  union, or absent nullable ApiValue?. Distinct token cases read successfully
  with general options. Web options cause int/string token ambiguity and a
  JsonException for the tested string.
- Two unclassified object cases serialize but reading fails JsonException.
  Structural classifier reads both cases by unique root names; unknown-only
  and both-case-property objects fail JsonException. A value equal to the other
  case's property name does not change selection.
- Reflection and generated closed inference register OrderEvent/FailureEvent,
  emit $type=OrderEvent/FailureEvent and round-trip both. Without generated
  inference the base writes {}; runtime flag alone throws InvalidOperationException
  for missing generated derived metadata.
- Per-type inference on an open type throws InvalidOperationException with
  reflection and produces error SYSLIB1243 with source generation.
- SerializeAsyncEnumerable default writes an array. topLevelValues=true writes
  compact JSONL despite WriteIndented=true and NewLine=CRLF. Hex byte 0A follows
  each object, including the last; no 0D. Two objects observed.

Exact JSONL bytes as escaped text:

```text
{"Seq":1,"Kind":"order"}\n{"Seq":2,"Kind":"failure"}\n
```

These are functional observations. No performance ranking or GA guarantee.
