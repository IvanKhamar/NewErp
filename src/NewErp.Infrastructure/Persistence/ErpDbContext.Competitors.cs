using Microsoft.EntityFrameworkCore;

using NewErp.Domain.Competitors;

namespace NewErp.Infrastructure.Persistence;

// Моніторинг цін конкурентів — наші таблиці erp_competitor*, на старому сайті таких немає.
public partial class ErpDbContext
{
    public DbSet<Competitor> Competitors => Set<Competitor>();

    public DbSet<CompetitorLink> CompetitorLinks => Set<CompetitorLink>();

    public DbSet<CompetitorPrice> CompetitorPrices => Set<CompetitorPrice>();

    private static void ConfigureCompetitors(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Competitor>(e =>
        {
            e.ToTable("erp_competitor");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
            e.Property(x => x.SiteUrl).HasColumnName("site_url").HasMaxLength(255);
            e.Property(x => x.PriceRegex).HasColumnName("price_regex").HasMaxLength(255);
            e.Property(x => x.IsActive).HasColumnName("active");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime");
            e.Ignore(x => x.Host);
        });

        modelBuilder.Entity<CompetitorLink>(e =>
        {
            e.ToTable("erp_competitor_link");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.CompetitorId).HasColumnName("competitor_id");
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.Url).HasColumnName("url").HasMaxLength(500);
            e.Property(x => x.LastPrice).HasColumnName("last_price").HasPrecision(15, 2);
            e.Property(x => x.LastCheckedAt).HasColumnName("last_checked_at").HasColumnType("datetime");
            e.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(255);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime");
        });

        modelBuilder.Entity<CompetitorPrice>(e =>
        {
            e.ToTable("erp_competitor_price");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.LinkId).HasColumnName("link_id");
            e.Property(x => x.Price).HasColumnName("price").HasPrecision(15, 2);
            e.Property(x => x.Source).HasColumnName("source").HasMaxLength(10);
            e.Property(x => x.CheckedAt).HasColumnName("checked_at").HasColumnType("datetime");
        });
    }
}
