using Microsoft.EntityFrameworkCore;

namespace NewErp.Infrastructure.Persistence;

// Вміст вітрини (пункт 50): інформаційні сторінки, FAQ і блог. Таблиці з db/reconstructed.sql, тож описані тут вручну.

public sealed class Information
{
    public int Id { get; set; }

    /// <summary>1 — посилання в підвалі вітрини.</summary>
    public int Bottom { get; set; }

    public int SortOrder { get; set; }

    public bool Status { get; set; } = true;
}

public sealed class InformationDescription
{
    public int InformationId { get; set; }

    public int LanguageId { get; set; }

    public string Title { get; set; } = "";

    public string Description { get; set; } = "";

    public string MetaTitle { get; set; } = "";

    public string MetaDescription { get; set; } = "";

    public string MetaKeyword { get; set; } = "";
}

public sealed class InformationToStore
{
    public int InformationId { get; set; }

    public int StoreId { get; set; }
}

public sealed class Faq
{
    public int Id { get; set; }

    public string Author { get; set; } = "";

    public string Email { get; set; } = "";

    public string? Comment { get; set; }

    public int SortOrder { get; set; }

    public bool Status { get; set; } = true;

    public DateTime DateAdded { get; set; }
}

public sealed class FaqDescription
{
    public int FaqId { get; set; }

    public int LanguageId { get; set; }

    /// <summary>Питання.</summary>
    public string Name { get; set; } = "";

    /// <summary>Відповідь (HTML).</summary>
    public string Description { get; set; } = "";
}

public sealed class Post
{
    public int Id { get; set; }

    public string Image { get; set; } = "";

    public string BigImage { get; set; } = "";

    public bool IsTop { get; set; }

    public bool Status { get; set; } = true;

    /// <summary>У старій базі може бути 0000-00-00: порівнювати в запиті можна, читати — ні.</summary>
    public DateTime DateAvailable { get; set; }

    public DateTime DateAdded { get; set; }
}

public sealed class PostDescription
{
    public int PostId { get; set; }

    public int LanguageId { get; set; }

    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    public string MetaTitle { get; set; } = "";

    public string MetaDescription { get; set; } = "";

    public string ImageAlt { get; set; } = "";
}

public sealed class PostCategory
{
    public int Id { get; set; }

    public bool Status { get; set; } = true;

    public DateTime DateAdded { get; set; }
}

public sealed class PostCategoryDescription
{
    public int PostCategoryId { get; set; }

    public int LanguageId { get; set; }

    public string Name { get; set; } = "";
}

public sealed class PostToCategory
{
    public int PostId { get; set; }

    public int PostCategoryId { get; set; }
}

public partial class ErpDbContext
{
    public DbSet<Information> Information => Set<Information>();

    public DbSet<InformationDescription> InformationDescriptions => Set<InformationDescription>();

    public DbSet<InformationToStore> InformationToStores => Set<InformationToStore>();

    public DbSet<Faq> Faqs => Set<Faq>();

    public DbSet<FaqDescription> FaqDescriptions => Set<FaqDescription>();

    public DbSet<Post> Posts => Set<Post>();

    public DbSet<PostDescription> PostDescriptions => Set<PostDescription>();

    public DbSet<PostCategory> PostCategories => Set<PostCategory>();

    public DbSet<PostCategoryDescription> PostCategoryDescriptions => Set<PostCategoryDescription>();

    public DbSet<PostToCategory> PostToCategories => Set<PostToCategory>();

    private static void ConfigureContent(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Information>(e =>
        {
            e.ToTable("dd_information");
            e.Property(x => x.Id).HasColumnName("information_id");
            e.Property(x => x.Bottom).HasColumnName("bottom");
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.Property(x => x.Status).HasColumnName("status");
        });
        modelBuilder.Entity<InformationDescription>(e =>
        {
            e.ToTable("dd_information_description");
            e.HasKey(x => new { x.InformationId, x.LanguageId });
            e.Property(x => x.InformationId).HasColumnName("information_id");
            e.Property(x => x.LanguageId).HasColumnName("language_id");
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(64);
            e.Property(x => x.Description).HasColumnName("description").HasColumnType("mediumtext");
            e.Property(x => x.MetaTitle).HasColumnName("meta_title").HasMaxLength(255);
            e.Property(x => x.MetaDescription).HasColumnName("meta_description").HasMaxLength(255);
            e.Property(x => x.MetaKeyword).HasColumnName("meta_keyword").HasMaxLength(255);
        });
        modelBuilder.Entity<InformationToStore>(e =>
        {
            e.ToTable("dd_information_to_store");
            e.HasKey(x => new { x.InformationId, x.StoreId });
            e.Property(x => x.InformationId).HasColumnName("information_id");
            e.Property(x => x.StoreId).HasColumnName("store_id");
        });
        modelBuilder.Entity<Faq>(e =>
        {
            e.ToTable("dd_faq");
            e.Property(x => x.Id).HasColumnName("faq_id");
            e.Property(x => x.Author).HasColumnName("author").HasMaxLength(64);
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(96);
            e.Property(x => x.Comment).HasColumnName("comment").HasColumnType("text");
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.DateAdded).HasColumnName("date_added").HasColumnType("datetime");
        });
        modelBuilder.Entity<FaqDescription>(e =>
        {
            e.ToTable("dd_faq_description");
            e.HasKey(x => new { x.FaqId, x.LanguageId });
            e.Property(x => x.FaqId).HasColumnName("faq_id");
            e.Property(x => x.LanguageId).HasColumnName("language_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(255);
            e.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        });
        modelBuilder.Entity<Post>(e =>
        {
            e.ToTable("dd_post");
            e.Property(x => x.Id).HasColumnName("post_id");
            e.Property(x => x.Image).HasColumnName("image").HasMaxLength(255);
            e.Property(x => x.BigImage).HasColumnName("big_image").HasMaxLength(255);
            e.Property(x => x.IsTop).HasColumnName("is_top");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.DateAvailable).HasColumnName("date_available").HasColumnType("datetime");
            e.Property(x => x.DateAdded).HasColumnName("date_added").HasColumnType("datetime");
        });
        modelBuilder.Entity<PostDescription>(e =>
        {
            e.ToTable("dd_post_description");
            e.HasKey(x => new { x.PostId, x.LanguageId });
            e.Property(x => x.PostId).HasColumnName("post_id");
            e.Property(x => x.LanguageId).HasColumnName("language_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(255);
            e.Property(x => x.Description).HasColumnName("description").HasColumnType("mediumtext");
            e.Property(x => x.MetaTitle).HasColumnName("meta_title").HasMaxLength(255);
            e.Property(x => x.MetaDescription).HasColumnName("meta_description").HasMaxLength(255);
            e.Property(x => x.ImageAlt).HasColumnName("image_alt").HasMaxLength(255);
        });
        modelBuilder.Entity<PostCategory>(e =>
        {
            e.ToTable("dd_post_category");
            e.Property(x => x.Id).HasColumnName("post_category_id");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.DateAdded).HasColumnName("date_added").HasColumnType("datetime");
        });
        modelBuilder.Entity<PostCategoryDescription>(e =>
        {
            e.ToTable("dd_post_category_description");
            e.HasKey(x => new { x.PostCategoryId, x.LanguageId });
            e.Property(x => x.PostCategoryId).HasColumnName("post_category_id");
            e.Property(x => x.LanguageId).HasColumnName("language_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(255);
        });
        modelBuilder.Entity<PostToCategory>(e =>
        {
            e.ToTable("dd_post_to_category");
            e.HasKey(x => new { x.PostId, x.PostCategoryId });
            e.Property(x => x.PostId).HasColumnName("post_id");
            e.Property(x => x.PostCategoryId).HasColumnName("post_category_id");
        });
    }
}
