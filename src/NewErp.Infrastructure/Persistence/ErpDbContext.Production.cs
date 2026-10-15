using Microsoft.EntityFrameworkCore;

using NewErp.Domain.Production;

namespace NewErp.Infrastructure.Persistence;

// Виробництво старого модуля StockTrack: таблиці dd_stock_production* (структура реконструйована, див. db/reconstructed.sql).
public partial class ErpDbContext
{
    public DbSet<ProductionModel> ProductionModels => Set<ProductionModel>();

    public DbSet<ProductionSize> ProductionSizes => Set<ProductionSize>();

    public DbSet<ProductionComponent> ProductionComponents => Set<ProductionComponent>();

    public DbSet<ComponentStock> ComponentStocks => Set<ComponentStock>();

    public DbSet<ComponentBomLine> ComponentBomLines => Set<ComponentBomLine>();

    public DbSet<SewnBomLine> SewnBomLines => Set<SewnBomLine>();

    private static void ConfigureProduction(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductionModel>(e =>
        {
            e.ToTable("dd_stock_production");
            e.Property(x => x.Id).HasColumnName("production_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(150);
            e.Property(x => x.Type).HasColumnName("type").HasMaxLength(32);
            e.Property(x => x.MinTotal).HasColumnName("min_total");
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.Property(x => x.IsActive).HasColumnName("status");
            e.Ignore(x => x.TotalSewn);

            // Колекції зберігаються в приватних полях (_sizes тощо). Рядок, прибраний з колекції, EF видаляє з бази.
            e.HasMany(x => x.Sizes).WithOne().HasForeignKey(x => x.ProductionId);
            e.HasMany(x => x.ProductLinks).WithOne().HasForeignKey(x => x.ProductionId);
            e.HasMany(x => x.CategoryLinks).WithOne().HasForeignKey(x => x.ProductionId);
            e.HasMany(x => x.SewnBom).WithOne().HasForeignKey(x => x.ProductionId);
            foreach (var navigation in new[] { nameof(ProductionModel.Sizes), nameof(ProductionModel.ProductLinks), nameof(ProductionModel.CategoryLinks), nameof(ProductionModel.SewnBom) })
                e.Navigation(navigation).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ProductionSize>(e =>
        {
            e.ToTable("dd_stock_production_sizes");
            e.Property(x => x.Id).HasColumnName("size_id");
            e.Property(x => x.ProductionId).HasColumnName("production_id");
            e.Property(x => x.Name).HasColumnName("size_name").HasMaxLength(32);
            e.Property(x => x.Percent).HasColumnName("percent").HasPrecision(5, 2);
            // Залишки — маркери паралельного доступу, як stock у розхідників: дві одночасні операції не перезапишуть одна одну.
            e.Property(x => x.CutStock).HasColumnName("cut_stock").HasPrecision(12, 3).IsConcurrencyToken();
            e.Property(x => x.SewnStock).HasColumnName("sewn_stock").HasPrecision(12, 3).IsConcurrencyToken();
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.Property(x => x.OptionValueId).HasColumnName("oc_option_value_id");
        });

        modelBuilder.Entity<ProductionProductLink>(e =>
        {
            e.ToTable("dd_stock_production_products");
            e.HasKey(x => new { x.ProductionId, x.ProductId });
            e.Property(x => x.ProductionId).HasColumnName("production_id");
            e.Property(x => x.ProductId).HasColumnName("product_id");
        });

        modelBuilder.Entity<ProductionCategoryLink>(e =>
        {
            e.ToTable("dd_stock_production_categories");
            e.HasKey(x => new { x.ProductionId, x.CategoryId });
            e.Property(x => x.ProductionId).HasColumnName("production_id");
            e.Property(x => x.CategoryId).HasColumnName("oc_category_id");
            e.Property(x => x.NameFilter).HasColumnName("name_filter").HasMaxLength(255);
            e.Property(x => x.MatchExact).HasColumnName("match_exact");
        });

        modelBuilder.Entity<SewnBomLine>(e =>
        {
            e.ToTable("dd_stock_production_sewn_bom");
            e.Property(x => x.Id).HasColumnName("bom_id");
            e.Property(x => x.ProductionId).HasColumnName("production_id");
            e.Property(x => x.SizeId).HasColumnName("size_id");
            e.Property(x => x.ComponentId).HasColumnName("component_id");
            e.Property(x => x.ItemId).HasColumnName("item_id");
            e.Property(x => x.QtyPerUnit).HasColumnName("qty_per_unit").HasPrecision(12, 4);
        });

        modelBuilder.Entity<ProductionComponent>(e =>
        {
            e.ToTable("dd_stock_production_components");
            e.Property(x => x.Id).HasColumnName("component_id");
            e.Property(x => x.ProductionId).HasColumnName("production_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.HasMany(x => x.Bom).WithOne().HasForeignKey(x => x.ComponentId);
            e.Navigation(x => x.Bom).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ComponentBomLine>(e =>
        {
            e.ToTable("dd_stock_production_component_bom");
            e.Property(x => x.Id).HasColumnName("bom_id");
            e.Property(x => x.ComponentId).HasColumnName("component_id");
            e.Property(x => x.SizeId).HasColumnName("size_id");
            e.Property(x => x.ItemId).HasColumnName("item_id");
            e.Property(x => x.QtyPerUnit).HasColumnName("qty_per_unit").HasPrecision(12, 4);
        });

        modelBuilder.Entity<ComponentStock>(e =>
        {
            e.ToTable("dd_stock_production_component_stock");
            e.HasKey(x => new { x.ComponentId, x.SizeId });
            e.Property(x => x.ComponentId).HasColumnName("component_id");
            e.Property(x => x.SizeId).HasColumnName("size_id");
            e.Property(x => x.Stock).HasColumnName("stock").HasPrecision(12, 3).IsConcurrencyToken();
        });
    }
}
