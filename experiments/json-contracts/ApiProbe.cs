// Compile-only probe: build for net8.0 / net10.0 / net11.0 without conditionals.
// Expected: compiles on net11.0; fails on older TFMs if SerializeAsyncEnumerable is absent (record the actual compiler error).
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace JsonContracts;

public static class ApiProbe
{
    public static Task WriteTopLevelValues(Stream stream, IAsyncEnumerable<int> items, JsonSerializerOptions? options) =>
        JsonSerializer.SerializeAsyncEnumerable(stream, items, topLevelValues: true, options: options);
}
