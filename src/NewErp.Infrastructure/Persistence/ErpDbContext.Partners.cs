using Microsoft.EntityFrameworkCore;

using NewErp.Domain.Partners;

namespace NewErp.Infrastructure.Persistence;

// Партнери-перепродавці старого сайту: dd_partner і відповідності (структура реконструйована, див. db/reconstructed.sql).
public partial class ErpDbContext
{
    public DbSet<Partner> Partners => Set<Partner>();

    private static void ConfigurePartners(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Partner>(e =>
        {
            e.ToTable("dd_partner");
            e.Property(x => x.Id).HasColumnName("partner_id");
            e.Property(x => x.Code).HasColumnName("code").HasMaxLength(32);
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(128);
            e.Property(x => x.IsActive).HasColumnName("status");
            e.Property(x => x.FeedUrl).HasColumnName("feed_url").HasMaxLength(255);
            e.Property(x => x.FeedEnabled).HasColumnName("feed_enabled");
            e.Property(x => x.VendorName).HasColumnName("vendor_name").HasMaxLength(64);
            e.Property(x => x.Notes).HasColumnName("notes").HasColumnType("text");
            e.Property(x => x.DefaultQuantity).HasColumnName("default_quantity");
            e.Property(x => x.DateAdded).HasColumnName("date_added").HasColumnType("datetime");
            e.Property(x => x.DateModified).HasColumnName("date_modified").HasColumnType("datetime");

            // Таблиці MyISAM без зовнішніх ключів: рядки відповідностей видаляє EF разом з партнером (каскад на боці EF).
            e.HasMany(x => x.CategoryMap).WithOne().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.ProductMap).WithOne().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.CategoryMap).UsePropertyAccessMode(PropertyAccessMode.Field);
            e.Navigation(x => x.ProductMap).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<PartnerCategoryMap>(e =>
        {
            e.ToTable("dd_partner_category_map");
            e.Property(x => x.Id).HasColumnName("partner_category_map_id");
            e.Property(x => x.PartnerId).HasColumnName("partner_id");
            e.Property(x => x.CategoryId).HasColumnName("category_id");
            e.Property(x => x.SizeName).HasColumnName("size_name").HasMaxLength(64);
            e.Property(x => x.ExternalId).HasColumnName("external_id").HasMaxLength(64);
            e.Property(x => x.ExternalName).HasColumnName("external_name").HasMaxLength(255);
            e.Property(x => x.PercentSupply).HasColumnName("percent_supply").HasPrecision(8, 2);
            e.Property(x => x.PercentSale).HasColumnName("percent_sale").HasPrecision(8, 2);
        });

        modelBuilder.Entity<PartnerProductMap>(e =>
        {
            e.ToTable("dd_partner_product_map");
            e.Property(x => x.Id).HasColumnName("partner_product_map_id");
            e.Property(x => x.PartnerId).HasColumnName("partner_id");
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.SizeName).HasColumnName("size_name").HasMaxLength(64);
            e.Property(x => x.ExternalId).HasColumnName("external_id").HasMaxLength(64);
            e.Property(x => x.ExternalName).HasColumnName("external_name").HasMaxLength(255);
            e.Property(x => x.PriceSupply).HasColumnName("price_supply").HasPrecision(15, 4);
            e.Property(x => x.PriceSale).HasColumnName("price_sale").HasPrecision(15, 4);
        });
    }
}
