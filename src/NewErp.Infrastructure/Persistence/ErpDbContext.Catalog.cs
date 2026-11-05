using Microsoft.EntityFrameworkCore;

namespace NewErp.Infrastructure.Persistence;

/// <summary>Рядок dd_product_special: акція на товар (стандартна таблиця OpenCart і колонка percent старого сайту).</summary>
public sealed class ProductSpecial
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int CustomerGroupId { get; set; }

    public int Priority { get; set; } = 1;

    public decimal Price { get; set; }

    public decimal Percent { get; set; }

    /// <summary>У базі може бути 0000-00-00 (без межі): порівнювати в запиті можна, читати — ні.</summary>
    public DateOnly DateStart { get; set; }

    public DateOnly DateEnd { get; set; }
}

// Частина partial-класу: вручну зіставлені таблиці каталогу, яких немає в згенерованому коді (db/reconstructed.sql).
public partial class ErpDbContext
{
    public DbSet<ProductSpecial> ProductSpecials => Set<ProductSpecial>();

    private static void ConfigureCatalog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductSpecial>(e =>
        {
            e.ToTable("dd_product_special");
            e.Property(x => x.Id).HasColumnName("product_special_id");
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.CustomerGroupId).HasColumnName("customer_group_id");
            e.Property(x => x.Priority).HasColumnName("priority");
            e.Property(x => x.Price).HasColumnName("price").HasPrecision(15, 4);
            e.Property(x => x.Percent).HasColumnName("percent").HasPrecision(5, 2);
            e.Property(x => x.DateStart).HasColumnName("date_start").HasColumnType("date");
            e.Property(x => x.DateEnd).HasColumnName("date_end").HasColumnType("date");
        });
    }
}
