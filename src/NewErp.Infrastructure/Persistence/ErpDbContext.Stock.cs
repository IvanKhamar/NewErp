using Microsoft.EntityFrameworkCore;

using NewErp.Domain.Stock;

namespace NewErp.Infrastructure.Persistence;

// Друга половина partial-класу. ErpDbContext.cs згенеровано командою
// dotnet ef dbcontext scaffold (таблиці dd_*), а тут вручну описано наші таблиці erp_stock_*,
// які зберігають доменні класи склада напряму. Перегенерація не зачіпає цей файл.
public partial class ErpDbContext
{
    public DbSet<StockCategory> StockCategories => Set<StockCategory>();

    public DbSet<StockSupplier> StockSuppliers => Set<StockSupplier>();

    public DbSet<StockItem> StockItems => Set<StockItem>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StockCategory>(e =>
        {
            e.ToTable("erp_stock_category");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
        });

        modelBuilder.Entity<StockSupplier>(e =>
        {
            e.ToTable("erp_stock_supplier");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(150);
            e.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(32);
            e.Property(x => x.Note).HasColumnName("note").HasMaxLength(500);
        });

        modelBuilder.Entity<StockItem>(e =>
        {
            e.ToTable("erp_stock_item");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.CategoryId).HasColumnName("category_id");
            e.Property(x => x.SupplierId).HasColumnName("supplier_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(150);
            e.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(16);
            // Маркер паралельного доступу: якщо двоє одночасно змінюють залишок, EF перевіряє,
            // що quantity в базі не змінилась з моменту читання, інакше кидає DbUpdateConcurrencyException (Api поверне 409).
            e.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(12, 3).IsConcurrencyToken();
            e.Property(x => x.MinQuantity).HasColumnName("min_quantity").HasPrecision(12, 3);
            e.Ignore(x => x.IsBelowMinimum);
            e.HasOne<StockCategory>().WithMany().HasForeignKey(x => x.CategoryId);
            e.HasOne<StockSupplier>().WithMany().HasForeignKey(x => x.SupplierId);
        });

        modelBuilder.Entity<StockMovement>(e =>
        {
            e.ToTable("erp_stock_movement");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ItemId).HasColumnName("item_id");
            e.Property(x => x.Delta).HasColumnName("delta").HasPrecision(12, 3);
            e.Property(x => x.QuantityAfter).HasColumnName("quantity_after").HasPrecision(12, 3);
            e.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(255);
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime");
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId);
        });
    }
}