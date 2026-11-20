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

// Частина partial-класу: таблиці довідників каталогу (пункти 47–48), яких немає в згенерованому коді (db/reconstructed.sql).
public partial class ErpDbContext
{
    public DbSet<SearchPreset> SearchPresets => Set<SearchPreset>();

    public DbSet<CategoryPath> CategoryPaths => Set<CategoryPath>();

    public DbSet<CategoryFilter> CategoryFilters => Set<CategoryFilter>();

    public DbSet<CategoryToStore> CategoryToStores => Set<CategoryToStore>();

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
    }
}
