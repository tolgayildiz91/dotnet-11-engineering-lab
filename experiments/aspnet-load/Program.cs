using System.Net;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateSlimBuilder(args);
builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(kestrel =>
    kestrel.Listen(IPAddress.Loopback, 33044, listener => listener.Protocols = HttpProtocols.Http1));

var products = new Dictionary<int, ProductResponse>();
for (var id = 1; id <= 1024; id++)
{
    products.Add(id, new(id, $"SYN-{id:D4}", $"Synthetic item {id}", 1000 + id, "USD", id % 37));
}

var app = builder.Build();
app.MapGet("/api/status/{id:int}", (int id) => TypedResults.Ok(new StatusResponse(id, "ok")));
app.MapGet("/api/products/{id:int}", (int id) =>
    products.TryGetValue(id, out var product) ? Results.Ok(product) : Results.NotFound());
app.MapGet("/api/orders/{id:int}", Order);

Console.WriteLine(JsonSerializer.Serialize(new
{
    runtime = RuntimeInformation.FrameworkDescription,
    core = Path.GetFileName(Path.GetDirectoryName(typeof(object).Assembly.Location)),
    aspnet = Path.GetFileName(Path.GetDirectoryName(typeof(WebApplication).Assembly.Location)),
    serverGc = GCSettings.IsServerGC
}));
app.Run();

static async Task<IResult> Order(int id)
{
    await Task.Yield();
    return TypedResults.Ok(new OrderResponse(id, "ready", id % 5 + 1));
}

record StatusResponse(int Id, string Status);
record ProductResponse(int Id, string Sku, string Name, int PriceCents, string Currency, int Stock);
record OrderResponse(int Id, string State, int ItemCount);
