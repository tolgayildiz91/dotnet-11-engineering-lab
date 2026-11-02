// Real loopback HTTP checks shared by both executables. Expected bodies are exact strings,
// so both implementations are held to byte-identical status, Location and JSON body.
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;

namespace Checkout.Model;

public static class HttpChecks
{
    public static readonly (string Name, CheckoutRequest Request, int Status, string? Location, string Body)[] Cases =
    {
        ("OrderCreated", new("SKU-1", 2, false), 201, "/orders/ord-SKU-1-2",
            """{"orderId":"ord-SKU-1-2","sku":"SKU-1","quantity":2,"total":25.00}"""),
        ("ValidationFailed", new("SKU-1", 0, false), 400, null,
            """{"field":"quantity","message":"Quantity must be positive."}"""),
        ("InventoryUnavailable", new("SKU-1", 9, false), 409, null,
            """{"sku":"SKU-1","requested":9,"available":5}"""),
        ("PaymentDeclined", new("SKU-1", 2, true), 402, null,
            """{"reasonCode":"synthetic_decline","amount":25.00}"""),
    };

    public static async Task<int> RunAsync(WebApplication app, string path)
    {
        app.Urls.Clear();
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        var address = app.Urls.Single();
        Console.WriteLine($"http bound {address}");
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        var failures = 0;
        foreach (var c in Cases)
        {
            using var response = await client.PostAsJsonAsync(path, c.Request);
            var body = await response.Content.ReadAsStringAsync();
            var status = (int)response.StatusCode;
            var location = response.Headers.Location?.OriginalString;
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            var ok = status == c.Status && location == c.Location && body == c.Body && mediaType == "application/json";
            Console.WriteLine($"http POST {path} {c.Name}: status={status} location={location ?? "-"} type={mediaType} body={body} -> {(ok ? "ok" : "MISMATCH")}");
            if (!ok) failures++;
        }
        await app.StopAsync();
        return failures;
    }
}
