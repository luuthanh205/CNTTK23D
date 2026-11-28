using System.Text.Json.Serialization;
using Inventory.Api.Data;
using Inventory.Api.Models;
using Inventory.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "inventory_tuan2.db");
builder.Services.AddDbContext<InventoryDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddScoped<InventoryService>();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;");
    if (!await db.InventoryItems.AnyAsync())
    {
        var now = DateTime.UtcNow;
        db.InventoryItems.AddRange(
            new InventoryItemEntity { Sku = "SP001", ProductName = "Laptop Dell XPS 13", Warehouse = "Kho Ha Noi", ServiceName = "OrderService", AvailableQuantity = 15, ReservedQuantity = 0, Status = StockStatus.Draft, Version = 1, UpdatedAt = now, History = [new InventoryHistoryEntity { Action = "Created", FromStatus = StockStatus.Draft, ToStatus = StockStatus.Draft, QuantityDelta = 15, PerformedBy = "seed", Note = "Khoi tao mau Tuan 2", Timestamp = now }] },
            new InventoryItemEntity { Sku = "SP002", ProductName = "Chuot Logitech MX Master 3", Warehouse = "Kho TP.HCM", ServiceName = "FulfillmentService", AvailableQuantity = 50, ReservedQuantity = 0, Status = StockStatus.Draft, Version = 1, UpdatedAt = now, History = [new InventoryHistoryEntity { Action = "Created", FromStatus = StockStatus.Draft, ToStatus = StockStatus.Draft, QuantityDelta = 50, PerformedBy = "seed", Note = "Khoi tao mau Tuan 2", Timestamp = now }] },
            new InventoryItemEntity { Sku = "SP003", ProductName = "Ban phim co Keychron K2", Warehouse = "Kho Da Nang", ServiceName = "OrderService", AvailableQuantity = 30, ReservedQuantity = 0, Status = StockStatus.Draft, Version = 1, UpdatedAt = now, History = [new InventoryHistoryEntity { Action = "Created", FromStatus = StockStatus.Draft, ToStatus = StockStatus.Draft, QuantityDelta = 30, PerformedBy = "seed", Note = "Khoi tao mau Tuan 2", Timestamp = now }] }
        );
        await db.SaveChangesAsync();
    }
}

app.MapGet("/inventory", async (string? warehouse, string? serviceName, StockStatus? status, InventoryService svc) => Results.Ok(await svc.GetAllAsync(warehouse, serviceName, status)));
app.MapGet("/inventory/{id:int}", async (int id, InventoryService svc) => (await svc.GetByIdAsync(id)) is var item && item is not null ? Results.Ok(item) : Results.NotFound(new { Message = "Khong tim thay ma ton kho." }));
app.MapPost("/inventory", async (HttpRequest req, CreateInventoryRequest body, InventoryService svc) =>
{
    var res = await svc.CreateAsync(body, req.Headers["Idempotency-Key"].FirstOrDefault());
    return res.Ok ? Results.Json(res.Data, statusCode: res.StatusCode) : Results.Json(new { Message = res.Error }, statusCode: res.StatusCode);
});
app.MapPost("/inventory/{id:int}/{action}", async (int id, string action, HttpRequest req, ActionRequest body, InventoryService svc) =>
{
    var res = await svc.ExecuteActionAsync(id, action, body, req.Headers["Idempotency-Key"].FirstOrDefault());
    return res.Ok ? Results.Json(res.Data, statusCode: res.StatusCode) : Results.Json(new { Message = res.Error }, statusCode: res.StatusCode);
});

app.Run("http://localhost:5227");
