using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NewErp.Infrastructure.Persistence;

// Таблиці форми товару старої адмінки (Python first, пункти 43 і 76), яких немає в згенерованому коді: стандартні
// таблиці OpenCart і власні таблиці старого сайту з db/reconstructed.sql.

/// <summary>dd_seo_url: адреса сторінки на вітрині; query — «product_id=5», «category_id=2»…</summary>
public sealed class SeoUrl
{
    public int Id { get; set; }

    public int StoreId { get; set; }

    public int LanguageId { get; set; }

    public string Query { get; set; } = "";

    public string Keyword { get; set; } = "";
}

/// <summary>dd_product_related: «З цим купують».</summary>
public sealed class ProductRelated
{
    public int ProductId { get; set; }

    public int RelatedId { get; set; }
}

/// <summary>dd_product_related_color (власна таблиця старого сайту): інші кольори того ж виробу, зв'язок в обидва боки.</summary>
public sealed class ProductRelatedColor
{
    public int ProductId { get; set; }

    public int RelatedId { get; set; }
}

/// <summary>dd_product_related_variant (власна таблиця старого сайту): варіанти того ж виробу, зв'язок в обидва боки.</summary>
public sealed class ProductRelatedVariant
{
    public int ProductId { get; set; }

    public int RelatedId { get; set; }
}

/// <summary>dd_product_color (власна таблиця старого сайту): назва кольору товару.</summary>
public sealed class ProductColor
{
    public int ProductId { get; set; }

    public int LanguageId { get; set; }

    public string Name { get; set; } = "";
}

/// <summary>dd_option: опція (розмір, колір, гравіювання) і її вид — select, radio, checkbox, image, text…</summary>
public sealed class CatalogOption
{
    public int OptionId { get; set; }

    public string Type { get; set; } = "";

    public int SortOrder { get; set; }
}

/// <summary>dd_option_value: значення опції зі списком.</summary>
public sealed class CatalogOptionValue
{
    public int OptionValueId { get; set; }

    public int OptionId { get; set; }

    public string Image { get; set; } = "";

    public int SortOrder { get; set; }
}

/// <summary>dd_attribute: атрибут товару («Склад») у групі атрибутів.</summary>
public sealed class CatalogAttribute
{
    public int AttributeId { get; set; }

    public int AttributeGroupId { get; set; }

    public int SortOrder { get; set; }
}

public sealed class CatalogAttributeDescription
{
    public int AttributeId { get; set; }

    public int LanguageId { get; set; }

    public string Name { get; set; } = "";
}

public sealed class CatalogAttributeGroupDescription
{
    public int AttributeGroupId { get; set; }

    public int LanguageId { get; set; }

    public string Name { get; set; } = "";
}

/// <summary>dd_product_attribute: значення атрибута товару («Льон 100%»).</summary>
public sealed class ProductAttribute
{
    public int ProductId { get; set; }

    public int AttributeId { get; set; }

    public int LanguageId { get; set; }

    public string Text { get; set; } = "";
}

public sealed class DownloadDescription
{
    public int DownloadId { get; set; }

    public int LanguageId { get; set; }

    public string Name { get; set; } = "";
}

public sealed class ProductToDownload
{
    public int ProductId { get; set; }

    public int DownloadId { get; set; }
}

public partial class ErpDbContext
{
    public DbSet<SeoUrl> SeoUrls => Set<SeoUrl>();

    public DbSet<ProductRelated> ProductRelateds => Set<ProductRelated>();

    public DbSet<ProductRelatedColor> ProductRelatedColors => Set<ProductRelatedColor>();

    public DbSet<ProductRelatedVariant> ProductRelatedVariants => Set<ProductRelatedVariant>();

    public DbSet<ProductColor> ProductColors => Set<ProductColor>();

    public DbSet<CatalogOption> CatalogOptions => Set<CatalogOption>();

    public DbSet<CatalogOptionValue> CatalogOptionValues => Set<CatalogOptionValue>();

    public DbSet<CatalogAttribute> CatalogAttributes => Set<CatalogAttribute>();

    public DbSet<CatalogAttributeDescription> CatalogAttributeDescriptions => Set<CatalogAttributeDescription>();

    public DbSet<CatalogAttributeGroupDescription> CatalogAttributeGroupDescriptions => Set<CatalogAttributeGroupDescription>();

    public DbSet<ProductAttribute> ProductAttributes => Set<ProductAttribute>();

    public DbSet<DownloadDescription> DownloadDescriptions => Set<DownloadDescription>();

    public DbSet<ProductToDownload> ProductToDownloads => Set<ProductToDownload>();

    private static void ConfigureProductForm(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SeoUrl>(e =>
        {
            e.ToTable("dd_seo_url");
            e.Property(x => x.Id).HasColumnName("seo_url_id");
            e.Property(x => x.StoreId).HasColumnName("store_id");
            e.Property(x => x.LanguageId).HasColumnName("language_id");
            e.Property(x => x.Query).HasColumnName("query").HasMaxLength(255);
            e.Property(x => x.Keyword).HasColumnName("keyword").HasMaxLength(255);
        });
        modelBuilder.Entity<ProductRelated>(e => Link(e, "dd_product_related", x => new { x.ProductId, x.RelatedId }));
        modelBuilder.Entity<ProductRelatedColor>(e => Link(e, "dd_product_related_color", x => new { x.ProductId, x.RelatedId }));
        modelBuilder.Entity<ProductRelatedVariant>(e => Link(e, "dd_product_related_variant", x => new { x.ProductId, x.RelatedId }));
        modelBuilder.Entity<ProductColor>(e =>
        {
            e.ToTable("dd_product_color");
            e.HasKey(x => new { x.ProductId, x.LanguageId });
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.LanguageId).HasColumnName("language_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(255);
        });
        modelBuilder.Entity<CatalogOption>(e =>
        {
            e.ToTable("dd_option");
            e.HasKey(x => x.OptionId);
            e.Property(x => x.OptionId).HasColumnName("option_id");
            e.Property(x => x.Type).HasColumnName("type").HasMaxLength(32);
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
        });
        modelBuilder.Entity<CatalogOptionValue>(e =>
        {
            e.ToTable("dd_option_value");
            e.HasKey(x => x.OptionValueId);
            e.Property(x => x.OptionValueId).HasColumnName("option_value_id");
            e.Property(x => x.OptionId).HasColumnName("option_id");
            e.Property(x => x.Image).HasColumnName("image").HasMaxLength(255);
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
        });
        modelBuilder.Entity<CatalogAttribute>(e =>
        {
            e.ToTable("dd_attribute");
            e.HasKey(x => x.AttributeId);
            e.Property(x => x.AttributeId).HasColumnName("attribute_id");
            e.Property(x => x.AttributeGroupId).HasColumnName("attribute_group_id");
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
        });
        modelBuilder.Entity<CatalogAttributeDescription>(e =>
        {
            e.ToTable("dd_attribute_description");
            e.HasKey(x => new { x.AttributeId, x.LanguageId });
            e.Property(x => x.AttributeId).HasColumnName("attribute_id");
            e.Property(x => x.LanguageId).HasColumnName("language_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(64);
        });
        modelBuilder.Entity<CatalogAttributeGroupDescription>(e =>
        {
            e.ToTable("dd_attribute_group_description");
            e.HasKey(x => new { x.AttributeGroupId, x.LanguageId });
            e.Property(x => x.AttributeGroupId).HasColumnName("attribute_group_id");
            e.Property(x => x.LanguageId).HasColumnName("language_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(64);
        });
        modelBuilder.Entity<ProductAttribute>(e =>
        {
            e.ToTable("dd_product_attribute");
            e.HasKey(x => new { x.ProductId, x.AttributeId, x.LanguageId });
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.AttributeId).HasColumnName("attribute_id");
            e.Property(x => x.LanguageId).HasColumnName("language_id");
            e.Property(x => x.Text).HasColumnName("text");
        });
        modelBuilder.Entity<DownloadDescription>(e =>
        {
            e.ToTable("dd_download_description");
            e.HasKey(x => new { x.DownloadId, x.LanguageId });
            e.Property(x => x.DownloadId).HasColumnName("download_id");
            e.Property(x => x.LanguageId).HasColumnName("language_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(64);
        });
        modelBuilder.Entity<ProductToDownload>(e =>
        {
            e.ToTable("dd_product_to_download");
            e.HasKey(x => new { x.ProductId, x.DownloadId });
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.DownloadId).HasColumnName("download_id");
        });
    }

    private static void Link<T>(EntityTypeBuilder<T> e, string table, Expression<Func<T, object?>> key) where T : class
    {
        e.ToTable(table);
        e.HasKey(key);
        e.Property<int>("ProductId").HasColumnName("product_id");
        e.Property<int>("RelatedId").HasColumnName("related_id");
    }
}
