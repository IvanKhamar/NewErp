using Microsoft.EntityFrameworkCore;

using NewErp.Domain.Stock;

namespace NewErp.Infrastructure.Persistence;

// Частина partial-класу. ErpDbContext.cs згенеровано командою dotnet ef dbcontext scaffold,
// а тут вручну зіставлено доменні класи складу з таблицями старого модуля StockTrack (dd_stock_*).
// Структуру цих таблиць реконструйовано (db/reconstructed.sql), тож назви колонок збігаються зі старим PHP-кодом.
public partial class ErpDbContext
{
    public DbSet<StockCategory> StockCategories => Set<StockCategory>();

    public DbSet<StockSupplier> StockSuppliers => Set<StockSupplier>();

    public DbSet<StockItem> StockItems => Set<StockItem>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    private static void ConfigureStock(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StockCategory>(e =>
        {
            e.ToTable("dd_stock_categories");
            e.Property(x => x.Id).HasColumnName("category_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
        });

        modelBuilder.Entity<StockSupplier>(e =>
        {
            e.ToTable("dd_stock_suppliers");
            e.Property(x => x.Id).HasColumnName("supplier_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(150);
            e.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(32);
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(96);
            e.Property(x => x.Categories).HasColumnName("categories").HasMaxLength(255);
            e.Property(x => x.Notes).HasColumnName("notes").HasColumnType("text");
        });

        modelBuilder.Entity<StockItem>(e =>
        {
            e.ToTable("dd_stock_items");
            e.Property(x => x.Id).HasColumnName("item_id");
            e.Property(x => x.CategoryId).HasColumnName("category_id");
            e.Property(x => x.SupplierId).HasColumnName("supplier_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(150);
            e.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(16);
            // Маркер паралельного доступу: якщо двоє одночасно змінюють залишок, EF перевіряє,
            // що stock у базі не змінився з моменту читання, інакше кидає DbUpdateConcurrencyException (Api поверне 409).
            e.Property(x => x.Quantity).HasColumnName("stock").HasPrecision(12, 3).IsConcurrencyToken();
            e.Property(x => x.MinQuantity).HasColumnName("min_stock").HasPrecision(12, 3);
            e.Property(x => x.UsedFor).HasColumnName("used_for").HasMaxLength(255);
            e.Property(x => x.Price).HasColumnName("price").HasPrecision(15, 4);
            e.Property(x => x.IsActive).HasColumnName("status");
            e.Ignore(x => x.IsBelowMinimum);
            e.HasOne<StockCategory>().WithMany().HasForeignKey(x => x.CategoryId);
            e.HasOne<StockSupplier>().WithMany().HasForeignKey(x => x.SupplierId);
        });

        modelBuilder.Entity<StockMovement>(e =>
        {
            e.ToTable("dd_stock_log");
            e.Property(x => x.Id).HasColumnName("log_id");
            e.Property(x => x.ItemId).HasColumnName("item_id");
            e.Property(x => x.ItemName).HasColumnName("item_name").HasMaxLength(150);
            e.Property(x => x.Action).HasColumnName("action").HasMaxLength(16);
            e.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(12, 3);
            e.Property(x => x.StockBefore).HasColumnName("stock_before").HasPrecision(12, 3);
            e.Property(x => x.StockAfter).HasColumnName("stock_after").HasPrecision(12, 3);
            e.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(255);
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Username).HasColumnName("username").HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasColumnName("date_added").HasColumnType("datetime");
            e.Ignore(x => x.Delta);
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId);
        });

        // Замовлення постачальнику — наші таблиці erp_purchase_order*, на старому сайті такого немає.
        modelBuilder.Entity<PurchaseOrder>(e =>
        {
            e.ToTable("erp_purchase_order");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.SupplierId).HasColumnName("supplier_id");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(16);
            e.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(500);
            e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime");
            e.Property(x => x.OrderedAt).HasColumnName("ordered_at").HasColumnType("datetime");
            e.Property(x => x.ClosedAt).HasColumnName("closed_at").HasColumnType("datetime");
            e.Ignore(x => x.IsOpen);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // Розхідники на окремих складах — наші таблиці erp_stock_location і erp_stock_transfer.
        modelBuilder.Entity<StockBalance>(e =>
        {
            e.ToTable("erp_stock_location");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ItemId).HasColumnName("item_id");
            e.Property(x => x.WarehouseId).HasColumnName("warehouse_id");
            // Як і в dd_stock_items: два одночасні переміщення з одного складу не пройдуть обидва.
            e.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(12, 3).IsConcurrencyToken();
        });

        modelBuilder.Entity<StockTransfer>(e =>
        {
            e.ToTable("erp_stock_transfer");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ItemId).HasColumnName("item_id");
            e.Property(x => x.FromWarehouseId).HasColumnName("from_warehouse_id");
            e.Property(x => x.ToWarehouseId).HasColumnName("to_warehouse_id");
            e.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(12, 3);
            e.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(255);
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Username).HasColumnName("username").HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime");
        });

        modelBuilder.Entity<PurchaseOrderLine>(e =>
        {
            e.ToTable("erp_purchase_order_line");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.ItemId).HasColumnName("item_id");
            e.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(12, 3);
            e.Property(x => x.Price).HasColumnName("price").HasPrecision(15, 4);
            e.Property(x => x.ReceivedQuantity).HasColumnName("received_quantity").HasPrecision(12, 3);
            e.Ignore(x => x.Remaining);
        });
    }
}
