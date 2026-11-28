using System.ComponentModel.DataAnnotations;

namespace Inventory.Api.Models;

public enum StockStatus { Draft, Reserved, Committed, Cancelled }

public class InventoryItemEntity
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Warehouse { get; set; } = string.Empty;
    public string ServiceName { get; set; } = "OrderService";
    public int AvailableQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public StockStatus Status { get; set; } = StockStatus.Draft;
    [ConcurrencyCheck]
    public int Version { get; set; } = 1;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<InventoryHistoryEntity> History { get; set; } = new();
}

public class InventoryHistoryEntity
{
    public int HistoryId { get; set; }
    public int InventoryItemId { get; set; }
    public string Action { get; set; } = string.Empty;
    public StockStatus FromStatus { get; set; }
    public StockStatus ToStatus { get; set; }
    public int QuantityDelta { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public record CreateInventoryRequest(string Sku, string ProductName, string Warehouse, string? ServiceName, int Quantity, string? PerformedBy, string? Note, string? IdempotencyKey);
public record ActionRequest(int Quantity, string PerformedBy, string? Note, int? ExpectedVersion, string? IdempotencyKey);
