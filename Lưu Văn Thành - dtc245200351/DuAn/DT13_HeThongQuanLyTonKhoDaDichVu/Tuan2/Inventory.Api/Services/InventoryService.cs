using Inventory.Api.Data;
using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Services;

public class OperationResult<T>
{
    public bool Ok { get; init; }
    public int StatusCode { get; init; }
    public string? Error { get; init; }
    public T? Data { get; init; }
    public static OperationResult<T> Success(T data, int code = 200) => new() { Ok = true, StatusCode = code, Data = data };
    public static OperationResult<T> Fail(int code, string error) => new() { Ok = false, StatusCode = code, Error = error };
}

public class InventoryService
{
    private readonly InventoryDbContext _db;
    public InventoryService(InventoryDbContext db) => _db = db;

    public async Task<List<InventoryItemEntity>> GetAllAsync(string? warehouse, string? serviceName, StockStatus? status)
    {
        var q = _db.InventoryItems.Include(x => x.History.OrderBy(h => h.HistoryId)).AsQueryable();
        if (!string.IsNullOrWhiteSpace(warehouse)) q = q.Where(x => x.Warehouse.ToLower().Contains(warehouse.Trim().ToLower()));
        if (!string.IsNullOrWhiteSpace(serviceName)) q = q.Where(x => x.ServiceName.ToLower() == serviceName.Trim().ToLower());
        if (status.HasValue) q = q.Where(x => x.Status == status.Value);
        return await q.OrderBy(x => x.Id).ToListAsync();
    }

    public async Task<InventoryItemEntity?> GetByIdAsync(int id) =>
        await _db.InventoryItems.Include(x => x.History.OrderBy(h => h.HistoryId)).FirstOrDefaultAsync(x => x.Id == id);

    public async Task<OperationResult<InventoryItemEntity>> CreateAsync(CreateInventoryRequest req, string? headerKey)
    {
        var key = !string.IsNullOrWhiteSpace(headerKey) ? headerKey : req.IdempotencyKey;
        if (!string.IsNullOrWhiteSpace(key))
        {
            var h = await _db.InventoryHistories.FirstOrDefaultAsync(x => x.IdempotencyKey == key);
            if (h is not null)
            {
                var exist = await GetByIdAsync(h.InventoryItemId);
                if (exist is not null) return OperationResult<InventoryItemEntity>.Success(exist, 200);
            }
        }
        if (string.IsNullOrWhiteSpace(req.Sku) || req.Quantity < 0)
            return OperationResult<InventoryItemEntity>.Fail(400, "Du lieu khong hop le.");

        var now = DateTime.UtcNow;
        var item = new InventoryItemEntity
        {
            Sku = req.Sku.Trim(),
            ProductName = req.ProductName.Trim(),
            Warehouse = req.Warehouse.Trim(),
            ServiceName = string.IsNullOrWhiteSpace(req.ServiceName) ? "OrderService" : req.ServiceName.Trim(),
            AvailableQuantity = req.Quantity,
            ReservedQuantity = 0,
            Status = StockStatus.Draft,
            Version = 1,
            UpdatedAt = now
        };
        item.History.Add(new InventoryHistoryEntity
        {
            Action = "Created", FromStatus = StockStatus.Draft, ToStatus = StockStatus.Draft,
            QuantityDelta = req.Quantity,
            PerformedBy = string.IsNullOrWhiteSpace(req.PerformedBy) ? "system" : req.PerformedBy,
            Note = string.IsNullOrWhiteSpace(req.Note) ? "Khoi tao ton kho Tuan 2" : req.Note,
            IdempotencyKey = key, Timestamp = now
        });
        _db.InventoryItems.Add(item);
        await _db.SaveChangesAsync();
        return OperationResult<InventoryItemEntity>.Success((await GetByIdAsync(item.Id))!, 201);
    }

    public async Task<OperationResult<InventoryItemEntity>> ExecuteActionAsync(int id, string action, ActionRequest req, string? headerKey)
    {
        var key = !string.IsNullOrWhiteSpace(headerKey) ? headerKey : req.IdempotencyKey;
        if (!string.IsNullOrWhiteSpace(key))
        {
            var dup = await _db.InventoryHistories.FirstOrDefaultAsync(h => h.InventoryItemId == id && h.IdempotencyKey == key);
            if (dup is not null)
            {
                var cur = await GetByIdAsync(id);
                if (cur is not null) return OperationResult<InventoryItemEntity>.Success(cur, 200);
            }
        }
        var item = await _db.InventoryItems.Include(x => x.History).FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return OperationResult<InventoryItemEntity>.Fail(404, "Khong tim thay ma ton kho.");
        if (req.ExpectedVersion.HasValue && req.ExpectedVersion.Value != item.Version)
            return OperationResult<InventoryItemEntity>.Fail(409, $"Xung dot phien ban: ExpectedVersion={req.ExpectedVersion.Value}, hien tai={item.Version}.");
        if (req.Quantity <= 0) return OperationResult<InventoryItemEntity>.Fail(400, "So luong phai lon hon 0.");

        var fromStatus = item.Status;
        StockStatus toStatus;
        int delta;
        switch (action.ToLower())
        {
            case "reserve":
                if (item.Status is not (StockStatus.Draft or StockStatus.Reserved))
                    return OperationResult<InventoryItemEntity>.Fail(409, $"Khong the Reserve tu trang thai {item.Status}.");
                if (item.AvailableQuantity < req.Quantity)
                    return OperationResult<InventoryItemEntity>.Fail(409, "Ton kho kha dung khong du.");
                item.AvailableQuantity -= req.Quantity;
                item.ReservedQuantity += req.Quantity;
                toStatus = StockStatus.Reserved;
                delta = -req.Quantity;
                break;
            case "commit":
                if (item.Status != StockStatus.Reserved)
                    return OperationResult<InventoryItemEntity>.Fail(409, $"Chi Commit duoc khi dang Reserved ^(hien tai: {item.Status}^).");
                if (item.ReservedQuantity < req.Quantity)
                    return OperationResult<InventoryItemEntity>.Fail(409, "So luong Commit vuot qua so luong giu cho.");
                item.ReservedQuantity -= req.Quantity;
                toStatus = StockStatus.Committed;
                delta = -req.Quantity;
                break;
            case "cancel":
                if (string.IsNullOrWhiteSpace(req.Note))
                    return OperationResult<InventoryItemEntity>.Fail(400, "Cancel bat buoc nhap Note ly do.");
                if (item.Status != StockStatus.Reserved)
                    return OperationResult<InventoryItemEntity>.Fail(409, $"Chi Cancel duoc khi dang Reserved ^(hien tai: {item.Status}^).");
                if (item.ReservedQuantity < req.Quantity)
                    return OperationResult<InventoryItemEntity>.Fail(409, "So luong huy vuot qua so luong giu cho.");
                item.ReservedQuantity -= req.Quantity;
                item.AvailableQuantity += req.Quantity;
                toStatus = StockStatus.Cancelled;
                delta = req.Quantity;
                break;
            default:
                return OperationResult<InventoryItemEntity>.Fail(400, $"Hanh dong khong ho tro: {action}");
        }

        var now = DateTime.UtcNow;
        item.Status = toStatus;
        item.Version += 1;
        item.UpdatedAt = now;
        item.History.Add(new InventoryHistoryEntity
        {
            InventoryItemId = item.Id,
            Action = char.ToUpper(action[0]) + action[1..].ToLower(),
            FromStatus = fromStatus, ToStatus = toStatus, QuantityDelta = delta,
            PerformedBy = string.IsNullOrWhiteSpace(req.PerformedBy) ? "worker" : req.PerformedBy,
            Note = string.IsNullOrWhiteSpace(req.Note) ? $"Thuc hien {action}" : req.Note,
            IdempotencyKey = key, Timestamp = now
        });

        try
        {
            await _db.SaveChangesAsync();
            return OperationResult<InventoryItemEntity>.Success((await GetByIdAsync(id))!, 200);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult<InventoryItemEntity>.Fail(409, "Xung dot cap nhat dong thoi.");
        }
    }
}
