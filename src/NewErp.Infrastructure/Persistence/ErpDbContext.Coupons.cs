using Microsoft.EntityFrameworkCore;

namespace NewErp.Infrastructure.Persistence;

/// <summary>Рядок dd_coupon_product: товар, на який діє купон.</summary>
public sealed class CouponProduct
{
    public int Id { get; set; }

    public int CouponId { get; set; }

    public int ProductId { get; set; }
}

/// <summary>Рядок dd_coupon_category: категорія, на яку діє купон.</summary>
public sealed class CouponCategory
{
    public int CouponId { get; set; }

    public int CategoryId { get; set; }
}

/// <summary>Рядок dd_coupon_product_exclude (таблиця старого сайту): товар, на який купон не діє ніколи.</summary>
public sealed class CouponProductExclude
{
    public int CouponId { get; set; }

    public int ProductId { get; set; }
}

/// <summary>Рядок dd_coupon_category_exclude (таблиця старого сайту): категорія, на яку купон не діє ніколи.</summary>
public sealed class CouponCategoryExclude
{
    public int CouponId { get; set; }

    public int CategoryId { get; set; }
}

/// <summary>Рядок dd_coupon_history: замовлення, що скористалося купоном.</summary>
public sealed class CouponHistory
{
    public int Id { get; set; }

    public int CouponId { get; set; }

    public int OrderId { get; set; }

    public int CustomerId { get; set; }

    public decimal Amount { get; set; }

    public DateTime DateAdded { get; set; }
}

// Частина partial-класу: таблиці купонів реконструйовано (db/reconstructed.sql), scaffold їх не знає.
public partial class ErpDbContext
{
    public DbSet<CouponProduct> CouponProducts => Set<CouponProduct>();

    public DbSet<CouponCategory> CouponCategories => Set<CouponCategory>();

    public DbSet<CouponProductExclude> CouponProductExcludes => Set<CouponProductExclude>();

    public DbSet<CouponCategoryExclude> CouponCategoryExcludes => Set<CouponCategoryExclude>();

    public DbSet<CouponHistory> CouponHistories => Set<CouponHistory>();

    private static void ConfigureCoupons(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CouponProduct>(e =>
        {
            e.ToTable("dd_coupon_product");
            e.Property(x => x.Id).HasColumnName("coupon_product_id");
            e.Property(x => x.CouponId).HasColumnName("coupon_id");
            e.Property(x => x.ProductId).HasColumnName("product_id");
        });
        modelBuilder.Entity<CouponCategory>(e =>
        {
            e.ToTable("dd_coupon_category");
            e.HasKey(x => new { x.CouponId, x.CategoryId });
            e.Property(x => x.CouponId).HasColumnName("coupon_id");
            e.Property(x => x.CategoryId).HasColumnName("category_id");
        });
        modelBuilder.Entity<CouponProductExclude>(e =>
        {
            e.ToTable("dd_coupon_product_exclude");
            e.HasKey(x => new { x.CouponId, x.ProductId });
            e.Property(x => x.CouponId).HasColumnName("coupon_id");
            e.Property(x => x.ProductId).HasColumnName("product_id");
        });
        modelBuilder.Entity<CouponCategoryExclude>(e =>
        {
            e.ToTable("dd_coupon_category_exclude");
            e.HasKey(x => new { x.CouponId, x.CategoryId });
            e.Property(x => x.CouponId).HasColumnName("coupon_id");
            e.Property(x => x.CategoryId).HasColumnName("category_id");
        });
        modelBuilder.Entity<CouponHistory>(e =>
        {
            e.ToTable("dd_coupon_history");
            e.Property(x => x.Id).HasColumnName("coupon_history_id");
            e.Property(x => x.CouponId).HasColumnName("coupon_id");
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.Amount).HasColumnName("amount").HasPrecision(15, 4);
            e.Property(x => x.DateAdded).HasColumnName("date_added").HasColumnType("datetime");
        });
    }
}
