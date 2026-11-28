using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Data;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }
    public DbSet<InventoryItemEntity> InventoryItems => Set<InventoryItemEntity>();
    public DbSet<InventoryHistoryEntity> InventoryHistories => Set<InventoryHistoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InventoryItemEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasMany(x => x.History).WithOne().HasForeignKey(h => h.InventoryItemId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InventoryHistoryEntity>(entity =>
        {
            entity.HasKey(x => x.HistoryId);
            entity.HasIndex(x => new { x.InventoryItemId, x.IdempotencyKey }).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
        });
    }
}
