using Microsoft.EntityFrameworkCore;

namespace NewErp.Infrastructure.Persistence;

/// <summary>Рядок dd_search_preset: готовий пошук вітрини (таблиця старого сайту).</summary>
public sealed class SearchPreset
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Url { get; set; } = "";

    public int SortOrder { get; set; }

    public bool Status { get; set; } = true;
}

/// <summary>Рядок dd_category_path: категорія з одним зі своїх предків (і собою) та його рівнем від кореня.</summary>
public sealed class CategoryPath
{
    public int CategoryId { get; set; }

    public int PathId { get; set; }

    public int Level { get; set; }
}

/// <summary>Рядок dd_category_filter: фільтр, який вітрина показує в категорії.</summary>
public sealed class CategoryFilter
{
    public int CategoryId { get; set; }

    public int FilterId { get; set; }
}

/// <summary>Рядок dd_category_to_store: категорія в магазині (у нас один, store_id = 0).</summary>
public sealed class CategoryToStore
{
    public int CategoryId { get; set; }

    public int StoreId { get; set; }
}


/// <summary>Рядок dd_filter_group: група фільтрів вітрини («Колір», «Розмір»).</summary>
public sealed class FilterGroup
{
    public int Id { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>Рядок dd_filter_group_description: назва групи фільтрів мовою.</summary>
public sealed class FilterGroupDescription
{
    public int FilterGroupId { get; set; }

    public int LanguageId { get; set; }

    public string Name { get; set; } = "";
}

/// <summary>Рядок dd_attribute_group: група атрибутів («Склад», «Догляд»).</summary>
public sealed class AttributeGroup
{
    public int Id { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>Рядок dd_manufacturer: виробник (бренд).</summary>
public sealed class Manufacturer
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string? Image { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>Рядок dd_manufacturer_to_store: виробник у магазині (у нас один, store_id = 0).</summary>
public sealed class ManufacturerToStore
{
    public int ManufacturerId { get; set; }

    public int StoreId { get; set; }
}

/// <summary>Рядок dd_download: файл для покупця (сам файл — у storage/download вітрини).</summary>
public sealed class Download
{
    public int Id { get; set; }

    public string Filename { get; set; } = "";

    public string Mask { get; set; } = "";

    public DateTime DateAdded { get; set; }
}
// Частина partial-класу: таблиці довідників каталогу (пункти 47–48), яких немає в згенерованому коді (db/reconstructed.sql).
public partial class ErpDbContext
{
    public DbSet<SearchPreset> SearchPresets => Set<SearchPreset>();

    public DbSet<CategoryPath> CategoryPaths => Set<CategoryPath>();

    public DbSet<CategoryFilter> CategoryFilters => Set<CategoryFilter>();

    public DbSet<CategoryToStore> CategoryToStores => Set<CategoryToStore>();

    public DbSet<FilterGroup> FilterGroups => Set<FilterGroup>();

    public DbSet<FilterGroupDescription> FilterGroupDescriptions => Set<FilterGroupDescription>();

    public DbSet<AttributeGroup> AttributeGroups => Set<AttributeGroup>();

    public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();

    public DbSet<ManufacturerToStore> ManufacturerToStores => Set<ManufacturerToStore>();

    public DbSet<Download> Downloads => Set<Download>();

    private static void ConfigureCatalogLists(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SearchPreset>(e =>
        {
            e.ToTable("dd_search_preset");
            e.Property(x => x.Id).HasColumnName("preset_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(255);
            e.Property(x => x.Url).HasColumnName("url").HasMaxLength(255);
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.Property(x => x.Status).HasColumnName("status");
        });

        modelBuilder.Entity<CategoryPath>(e =>
        {
            e.ToTable("dd_category_path");
            e.HasKey(x => new { x.CategoryId, x.PathId });
            e.Property(x => x.CategoryId).HasColumnName("category_id");
            e.Property(x => x.PathId).HasColumnName("path_id");
            e.Property(x => x.Level).HasColumnName("level");
        });

        modelBuilder.Entity<CategoryFilter>(e =>
        {
            e.ToTable("dd_category_filter");
            e.HasKey(x => new { x.CategoryId, x.FilterId });
            e.Property(x => x.CategoryId).HasColumnName("category_id");
            e.Property(x => x.FilterId).HasColumnName("filter_id");
        });

        modelBuilder.Entity<CategoryToStore>(e =>
        {
            e.ToTable("dd_category_to_store");
            e.HasKey(x => new { x.CategoryId, x.StoreId });
            e.Property(x => x.CategoryId).HasColumnName("category_id");
            e.Property(x => x.StoreId).HasColumnName("store_id");
        });

        modelBuilder.Entity<FilterGroup>(e =>
        {
            e.ToTable("dd_filter_group");
            e.Property(x => x.Id).HasColumnName("filter_group_id");
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
        });

        modelBuilder.Entity<FilterGroupDescription>(e =>
        {
            e.ToTable("dd_filter_group_description");
            e.HasKey(x => new { x.FilterGroupId, x.LanguageId });
            e.Property(x => x.FilterGroupId).HasColumnName("filter_group_id");
            e.Property(x => x.LanguageId).HasColumnName("language_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(64);
        });

        modelBuilder.Entity<AttributeGroup>(e =>
        {
            e.ToTable("dd_attribute_group");
            e.Property(x => x.Id).HasColumnName("attribute_group_id");
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
        });

        modelBuilder.Entity<Manufacturer>(e =>
        {
            e.ToTable("dd_manufacturer");
            e.Property(x => x.Id).HasColumnName("manufacturer_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(64);
            e.Property(x => x.Image).HasColumnName("image").HasMaxLength(255);
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
        });

        modelBuilder.Entity<ManufacturerToStore>(e =>
        {
            e.ToTable("dd_manufacturer_to_store");
            e.HasKey(x => new { x.ManufacturerId, x.StoreId });
            e.Property(x => x.ManufacturerId).HasColumnName("manufacturer_id");
            e.Property(x => x.StoreId).HasColumnName("store_id");
        });

        modelBuilder.Entity<Download>(e =>
        {
            e.ToTable("dd_download");
            e.Property(x => x.Id).HasColumnName("download_id");
            e.Property(x => x.Filename).HasColumnName("filename").HasMaxLength(160);
            e.Property(x => x.Mask).HasColumnName("mask").HasMaxLength(128);
            e.Property(x => x.DateAdded).HasColumnName("date_added").HasColumnType("datetime");
        });
    }
}
