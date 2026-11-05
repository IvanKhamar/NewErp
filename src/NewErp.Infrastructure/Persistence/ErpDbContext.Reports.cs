using Microsoft.EntityFrameworkCore;

namespace NewErp.Infrastructure.Persistence;

/// <summary>
/// Рядок dd_remarketing_orders (таблиця модуля вітрини). Час 0000-00-00 00:00:00 означає «подію не надіслано»: читати такий
/// час у DateTime не можна, тому в запитах він стає null через CASE.
/// </summary>
public sealed class RemarketingOrder
{
    public int OrderId { get; set; }

    public string FirstReferrer { get; set; } = "";

    public string LastReferrer { get; set; } = "";

    public string UtmSource { get; set; } = "";

    public string UtmMedium { get; set; } = "";

    public string UtmCampaign { get; set; } = "";

    public string UtmContent { get; set; } = "";

    public string UtmTerm { get; set; } = "";

    public string UtmReferrer { get; set; } = "";

    public DateTime SuccessPage { get; set; }

    public DateTime Ga4 { get; set; }

    public DateTime Facebook { get; set; }

    public DateTime FacebookLead { get; set; }

    public DateTime Esputnik { get; set; }

    public DateTime Telegram { get; set; }

    public DateTime Tiktok { get; set; }
}

public sealed class CustomerOnline
{
    public string Ip { get; set; } = "";

    public int CustomerId { get; set; }

    public string Url { get; set; } = "";

    public string Referer { get; set; } = "";

    public DateTime DateAdded { get; set; }
}

public sealed class CustomerReward
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public int OrderId { get; set; }

    public string Description { get; set; } = "";

    public int Points { get; set; }

    public DateTime DateAdded { get; set; }
}

public sealed class CustomerTransaction
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public int OrderId { get; set; }

    public string Description { get; set; } = "";

    public decimal Amount { get; set; }

    public DateTime DateAdded { get; set; }
}

public sealed class CustomerActivity
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public string Key { get; set; } = "";

    public string Data { get; set; } = "";

    public string Ip { get; set; } = "";

    public DateTime DateAdded { get; set; }
}

public sealed class CustomerSearch
{
    public int Id { get; set; }

    public int StoreId { get; set; }

    public int LanguageId { get; set; }

    public int CustomerId { get; set; }

    public string Keyword { get; set; } = "";

    public int? CategoryId { get; set; }

    public bool SubCategory { get; set; }

    public bool Description { get; set; }

    public int Products { get; set; }

    public string Ip { get; set; } = "";

    public DateTime DateAdded { get; set; }
}

public sealed class StoreStatistic
{
    public int Id { get; set; }

    public string Code { get; set; } = "";

    public decimal Value { get; set; }
}

// Частина partial-класу: таблиці звітів реконструйовано (db/reconstructed.sql), scaffold їх не знає.
public partial class ErpDbContext
{
    public DbSet<RemarketingOrder> RemarketingOrders => Set<RemarketingOrder>();

    public DbSet<CustomerOnline> CustomersOnline => Set<CustomerOnline>();

    public DbSet<CustomerReward> CustomerRewards => Set<CustomerReward>();

    public DbSet<CustomerTransaction> CustomerTransactions => Set<CustomerTransaction>();

    public DbSet<CustomerActivity> CustomerActivities => Set<CustomerActivity>();

    public DbSet<CustomerSearch> CustomerSearches => Set<CustomerSearch>();

    public DbSet<StoreStatistic> StoreStatistics => Set<StoreStatistic>();

    private static void ConfigureReports(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RemarketingOrder>(e =>
        {
            e.ToTable("dd_remarketing_orders");
            e.HasKey(x => x.OrderId);
            e.Property(x => x.OrderId).HasColumnName("order_id").ValueGeneratedNever();
            e.Property(x => x.FirstReferrer).HasColumnName("first_referrer").HasMaxLength(255);
            e.Property(x => x.LastReferrer).HasColumnName("last_referrer").HasMaxLength(255);
            e.Property(x => x.UtmSource).HasColumnName("utm_source").HasMaxLength(255);
            e.Property(x => x.UtmMedium).HasColumnName("utm_medium").HasMaxLength(255);
            e.Property(x => x.UtmCampaign).HasColumnName("utm_campaign").HasMaxLength(255);
            e.Property(x => x.UtmContent).HasColumnName("utm_content").HasMaxLength(255);
            e.Property(x => x.UtmTerm).HasColumnName("utm_term").HasMaxLength(255);
            e.Property(x => x.UtmReferrer).HasColumnName("utm_referrer").HasMaxLength(255);
            e.Property(x => x.SuccessPage).HasColumnName("success_page").HasColumnType("datetime");
            e.Property(x => x.Ga4).HasColumnName("ga4").HasColumnType("datetime");
            e.Property(x => x.Facebook).HasColumnName("facebook").HasColumnType("datetime");
            e.Property(x => x.FacebookLead).HasColumnName("facebook_lead").HasColumnType("datetime");
            e.Property(x => x.Esputnik).HasColumnName("esputnik").HasColumnType("datetime");
            e.Property(x => x.Telegram).HasColumnName("telegram").HasColumnType("datetime");
            e.Property(x => x.Tiktok).HasColumnName("tiktok").HasColumnType("datetime");
        });
        modelBuilder.Entity<CustomerOnline>(e =>
        {
            e.ToTable("dd_customer_online");
            e.HasKey(x => x.Ip);
            e.Property(x => x.Ip).HasColumnName("ip").HasMaxLength(40);
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.Url).HasColumnName("url").HasColumnType("text");
            e.Property(x => x.Referer).HasColumnName("referer").HasColumnType("text");
            e.Property(x => x.DateAdded).HasColumnName("date_added").HasColumnType("datetime");
        });
        modelBuilder.Entity<CustomerReward>(e =>
        {
            e.ToTable("dd_customer_reward");
            e.Property(x => x.Id).HasColumnName("customer_reward_id");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
            e.Property(x => x.Points).HasColumnName("points");
            e.Property(x => x.DateAdded).HasColumnName("date_added").HasColumnType("datetime");
        });
        modelBuilder.Entity<CustomerTransaction>(e =>
        {
            e.ToTable("dd_customer_transaction");
            e.Property(x => x.Id).HasColumnName("customer_transaction_id");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
            e.Property(x => x.Amount).HasColumnName("amount").HasPrecision(15, 4);
            e.Property(x => x.DateAdded).HasColumnName("date_added").HasColumnType("datetime");
        });
        modelBuilder.Entity<CustomerActivity>(e =>
        {
            e.ToTable("dd_customer_activity");
            e.Property(x => x.Id).HasColumnName("customer_activity_id");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.Key).HasColumnName("key").HasMaxLength(64);
            e.Property(x => x.Data).HasColumnName("data").HasColumnType("text");
            e.Property(x => x.Ip).HasColumnName("ip").HasMaxLength(40);
            e.Property(x => x.DateAdded).HasColumnName("date_added").HasColumnType("datetime");
        });
        modelBuilder.Entity<CustomerSearch>(e =>
        {
            e.ToTable("dd_customer_search");
            e.Property(x => x.Id).HasColumnName("customer_search_id");
            e.Property(x => x.StoreId).HasColumnName("store_id");
            e.Property(x => x.LanguageId).HasColumnName("language_id");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.Keyword).HasColumnName("keyword").HasMaxLength(255);
            e.Property(x => x.CategoryId).HasColumnName("category_id");
            e.Property(x => x.SubCategory).HasColumnName("sub_category");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.Products).HasColumnName("products");
            e.Property(x => x.Ip).HasColumnName("ip").HasMaxLength(40);
            e.Property(x => x.DateAdded).HasColumnName("date_added").HasColumnType("datetime");
        });
        modelBuilder.Entity<StoreStatistic>(e =>
        {
            e.ToTable("dd_statistics");
            e.Property(x => x.Id).HasColumnName("statistics_id");
            e.Property(x => x.Code).HasColumnName("code").HasMaxLength(64);
            e.Property(x => x.Value).HasColumnName("value").HasPrecision(15, 4);
        });
    }
}
