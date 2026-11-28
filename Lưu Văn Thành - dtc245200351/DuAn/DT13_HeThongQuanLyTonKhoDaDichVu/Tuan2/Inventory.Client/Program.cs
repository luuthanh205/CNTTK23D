using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

Console.OutputEncoding = Encoding.UTF8;
using var client = new HttpClient { BaseAddress = new Uri("http://localhost:5227") };

Console.WriteLine("=== DEMO TUAN 2 ===");
var r1 = await client.PostAsJsonAsync("/inventory/1/reserve", new { Quantity = 4, PerformedBy = "OrderService", Note = "Giu cho SP001", ExpectedVersion = 1, IdempotencyKey = "idem-1" });
Console.WriteLine($"Reserve lan 1: {(int)r1.StatusCode}");
var r2 = await client.PostAsJsonAsync("/inventory/1/reserve", new { Quantity = 4, PerformedBy = "OrderService", Note = "Lap lai key", ExpectedVersion = 2, IdempotencyKey = "idem-1" });
Console.WriteLine($"Reserve lap lai key: {(int)r2.StatusCode}");

var tCommit = client.PostAsJsonAsync("/inventory/1/commit", new { Quantity = 4, PerformedBy = "BillingService", Note = "Xuat kho", ExpectedVersion = 2 });
var tCancel = client.PostAsJsonAsync("/inventory/1/cancel", new { Quantity = 4, PerformedBy = "TimeoutService", Note = "Huy do qua han", ExpectedVersion = 2 });
var both = await Task.WhenAll(tCommit, tCancel);
Console.WriteLine($"Dong thoi -^> Commit: {(int)both[0].StatusCode} ^| Cancel: {(int)both[1].StatusCode}");

var detail = await client.GetStringAsync("/inventory/1");
using var doc = JsonDocument.Parse(detail);
Console.WriteLine(JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true }));
