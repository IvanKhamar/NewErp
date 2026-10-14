using Microsoft.EntityFrameworkCore;

using NewErp.Domain.Production;
using NewErp.Domain.Stock;

namespace NewErp.Infrastructure.Persistence;

// Таблиці erp_bom*, які зберігають специфікації виробів (доменні класи Bom і BomLine).
public partial class ErpDbContext
{
    public DbSet<Bom> Boms => Set<Bom>();

    public DbSet<BomLine> BomLines => Set<BomLine>();

    private static void ConfigureProduction(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bom>(e =>
        {
            e.ToTable("erp_bom");
            e.HasKey(x => x.ProductId);
            // product_id задаємо самі (це id товару OpenCart), а не генерує база.
            e.Property(x => x.ProductId).HasColumnName("product_id").ValueGeneratedNever();
            e.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime");
            // Рядки зберігаються в приватному полі _lines. Рядок, прибраний з колекції, EF видаляє з бази.
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<BomLine>(e =>
        {
            e.ToTable("erp_bom_line");
            e.HasKey(x => new { x.ProductId, x.StockItemId });
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.StockItemId).HasColumnName("stock_item_id");
            e.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(12, 3);
            e.HasOne<StockItem>().WithMany().HasForeignKey(x => x.StockItemId);
        });
    }
}
