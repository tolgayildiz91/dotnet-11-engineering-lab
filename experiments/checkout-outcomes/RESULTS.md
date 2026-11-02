# Recorded RC3 observations

Release, X64 Windows, SDK11.0.100-rc.3.26522.148, Roslyn5.12.0-1.26522.148. All three applications built with zero warnings/errors and completed their semantic and loopback HTTP checks. The full verification had zero failed checks and two OS-blocked standalone fixture launches, so its exit code was2.

| Compiler fixture | Actual observation |
|---|---|
| Complete union switch | No warning |
| Omit PaymentDeclined | Warning CS8509, successful build |
| Same omission, warnings-as-errors | Error CS8509, failed build |
| Fifth union case, unchanged recompiled consumer | Warning CS8509; new value throws SwitchExpressionException |
| Nullable case, missing null arm | Warning CS8655 |
| Default union, four nonnullable case arms | No warning; default input throws SwitchExpressionException |
| Open baseline error hierarchy, no fallback | Warning CS8509 |
| Fifth baseline error, existing fallback | No warning; new value reaches fallback |

The two refused standalone launches were native-complete and baseline-no-discard (Windows Application Control0x800711C7). Their build observations were verified. Identical Consumer.cs against an identical four-case union declaration also ran inside the Native application, checking all four complete expected strings. No runtime behavior is claimed for the refused baseline program. The security policy was not changed. Generated evidence remains local and is not part of this package.

All12 HTTP requests across the three application targets matched exact status, Location, media type and bodies. Both business representations required explicit transport mapping.

JSON writing emitted active case bodies and null for a default union. Reading the four object cases without a classifier threw ambiguity JsonException. A structural classifier passed payload equality and byte-exact reserialization for each fixture body. These shapes are distinguishable (two share sku); overlapping/subset shapes and additive evolution were not tested. Amount(int,string) read number/string under default options; the tested string was ambiguous under Web options.

Reflection found one object field in each generated union. In one focused construction count,100000 int-case conversions allocated2400000 bytes (24/conversion) for generated Amount and0 for the hand-written custom union. This is not a statistical timing benchmark or a Result allocation comparison. No inference about whole-checkout performance, reference payload creation or transport allocation follows.

The useful distinction is a closed case set with recompilation warnings versus reusable binary success/error composition. A Result can use a closed error model, including a union; a multi-handler Match API can require every handler. Those alternatives were not executed as separate comparisons. Existing binaries and consumers with catch-all arms receive no new compile-time protection from this experiment.
