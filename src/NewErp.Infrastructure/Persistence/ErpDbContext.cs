using System;
using System.Collections.Generic;

using Microsoft.EntityFrameworkCore;

using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.Infrastructure.Persistence;

public partial class ErpDbContext : DbContext
{
    public ErpDbContext(DbContextOptions<ErpDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<DdAddress> DdAddresses { get; set; }

    public virtual DbSet<DdCategory> DdCategories { get; set; }

    public virtual DbSet<DdCategoryDescription> DdCategoryDescriptions { get; set; }

    public virtual DbSet<DdCoupon> DdCoupons { get; set; }

    public virtual DbSet<DdCrosssellItem> DdCrosssellItems { get; set; }

    public virtual DbSet<DdCrosssellSort> DdCrosssellSorts { get; set; }

    public virtual DbSet<DdCustomer> DdCustomers { get; set; }

    public virtual DbSet<DdCustomerGroupDescription> DdCustomerGroupDescriptions { get; set; }

    public virtual DbSet<DdCustomerHistory> DdCustomerHistories { get; set; }

    public virtual DbSet<DdFilter> DdFilters { get; set; }

    public virtual DbSet<DdFilterDescription> DdFilterDescriptions { get; set; }

    public virtual DbSet<DdOptionDescription> DdOptionDescriptions { get; set; }

    public virtual DbSet<DdOptionValueDescription> DdOptionValueDescriptions { get; set; }

    public virtual DbSet<DdOrder> DdOrders { get; set; }

    public virtual DbSet<DdOrderOption> DdOrderOptions { get; set; }

    public virtual DbSet<DdOrderProduct> DdOrderProducts { get; set; }

    public virtual DbSet<DdOrderSource> DdOrderSources { get; set; }

    public virtual DbSet<DdOrderStatus> DdOrderStatuses { get; set; }

    public virtual DbSet<DdOrderStatusLog> DdOrderStatusLogs { get; set; }

    public virtual DbSet<DdOrderTotal> DdOrderTotals { get; set; }

    public virtual DbSet<DdProduct> DdProducts { get; set; }

    public virtual DbSet<DdProductDescription> DdProductDescriptions { get; set; }

    public virtual DbSet<DdProductFilter> DdProductFilters { get; set; }

    public virtual DbSet<DdProductImage> DdProductImages { get; set; }

    public virtual DbSet<DdProductLabel> DdProductLabels { get; set; }

    public virtual DbSet<DdProductManualPin> DdProductManualPins { get; set; }

    public virtual DbSet<DdProductOption> DdProductOptions { get; set; }

    public virtual DbSet<DdProductOptionValue> DdProductOptionValues { get; set; }

    public virtual DbSet<DdProductOrderSnapshot> DdProductOrderSnapshots { get; set; }

    public virtual DbSet<DdProductOrderSnapshotItem> DdProductOrderSnapshotItems { get; set; }

    public virtual DbSet<DdProductToCategory> DdProductToCategories { get; set; }

    public virtual DbSet<DdProductToLabel> DdProductToLabels { get; set; }

    public virtual DbSet<DdProductToStore> DdProductToStores { get; set; }

    public virtual DbSet<DdQuestion> DdQuestions { get; set; }

    public virtual DbSet<DdReturn> DdReturns { get; set; }

    public virtual DbSet<DdReturnAction> DdReturnActions { get; set; }

    public virtual DbSet<DdReturnHistory> DdReturnHistories { get; set; }

    public virtual DbSet<DdReturnReason> DdReturnReasons { get; set; }

    public virtual DbSet<DdReturnStatus> DdReturnStatuses { get; set; }

    public virtual DbSet<DdSetting> DdSettings { get; set; }

    public virtual DbSet<DdSubscribe> DdSubscribes { get; set; }

    public virtual DbSet<DdUser> DdUsers { get; set; }

    public virtual DbSet<ErpOrderManager> ErpOrderManagers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DdAddress>(entity =>
        {
            entity.HasKey(e => e.AddressId).HasName("PRIMARY");

            entity.ToTable("dd_address");

            entity.HasIndex(e => e.CustomerId, "customer_id");

            entity.Property(e => e.AddressId).HasColumnName("address_id");
            entity.Property(e => e.Address1)
                .HasMaxLength(128)
                .HasColumnName("address_1");
            entity.Property(e => e.Address2)
                .HasMaxLength(128)
                .HasColumnName("address_2");
            entity.Property(e => e.City)
                .HasMaxLength(128)
                .HasColumnName("city");
            entity.Property(e => e.Company)
                .HasMaxLength(40)
                .HasColumnName("company");
            entity.Property(e => e.CountryId).HasColumnName("country_id");
            entity.Property(e => e.CustomField)
                .HasColumnType("text")
                .HasColumnName("custom_field");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.Firstname)
                .HasMaxLength(32)
                .HasColumnName("firstname");
            entity.Property(e => e.Lastname)
                .HasMaxLength(32)
                .HasColumnName("lastname");
            entity.Property(e => e.Postcode)
                .HasMaxLength(10)
                .HasColumnName("postcode");
            entity.Property(e => e.ZoneId).HasColumnName("zone_id");
        });

        modelBuilder.Entity<DdCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PRIMARY");

            entity.ToTable("dd_category");

            entity.HasIndex(e => e.ParentId, "parent_id");

            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.Column).HasColumnName("column");
            entity.Property(e => e.DateAdded)
                .HasColumnType("datetime")
                .HasColumnName("date_added");
            entity.Property(e => e.DateModified)
                .HasColumnType("datetime")
                .HasColumnName("date_modified");
            entity.Property(e => e.Image)
                .HasMaxLength(255)
                .HasColumnName("image");
            entity.Property(e => e.ParentId).HasColumnName("parent_id");
            entity.Property(e => e.ProductionPeriod).HasColumnName("production_period");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.Top).HasColumnName("top");
        });

        modelBuilder.Entity<DdCategoryDescription>(entity =>
        {
            entity.HasKey(e => new { e.CategoryId, e.LanguageId }).HasName("PRIMARY");

            entity.ToTable("dd_category_description");

            entity.HasIndex(e => e.Name, "name");

            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.LanguageId).HasColumnName("language_id");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.MetaDescription)
                .HasMaxLength(255)
                .HasColumnName("meta_description");
            entity.Property(e => e.MetaKeyword)
                .HasMaxLength(255)
                .HasColumnName("meta_keyword");
            entity.Property(e => e.MetaTitle)
                .HasMaxLength(255)
                .HasColumnName("meta_title");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        modelBuilder.Entity<DdCoupon>(entity =>
        {
            entity.HasKey(e => e.CouponId).HasName("PRIMARY");

            entity.ToTable("dd_coupon");

            entity.Property(e => e.CouponId).HasColumnName("coupon_id");
            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .HasColumnName("code");
            entity.Property(e => e.DateAdded)
                .HasColumnType("datetime")
                .HasColumnName("date_added");
            entity.Property(e => e.DateEnd)
                .HasDefaultValueSql("'0000-00-00'")
                .HasColumnType("date")
                .HasColumnName("date_end");
            entity.Property(e => e.DateStart)
                .HasDefaultValueSql("'0000-00-00'")
                .HasColumnType("date")
                .HasColumnName("date_start");
            entity.Property(e => e.Discount)
                .HasPrecision(15, 4)
                .HasColumnName("discount");
            entity.Property(e => e.Logged).HasColumnName("logged");
            entity.Property(e => e.Name)
                .HasMaxLength(128)
                .HasColumnName("name");
            entity.Property(e => e.Shipping).HasColumnName("shipping");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.Total)
                .HasPrecision(15, 4)
                .HasColumnName("total");
            entity.Property(e => e.Type)
                .HasMaxLength(1)
                .IsFixedLength()
                .HasColumnName("type");
            entity.Property(e => e.UsesCustomer)
                .HasMaxLength(11)
                .HasColumnName("uses_customer");
            entity.Property(e => e.UsesTotal).HasColumnName("uses_total");
        });

        modelBuilder.Entity<DdCrosssellItem>(entity =>
        {
            entity.HasKey(e => e.CrosssellItemId).HasName("PRIMARY");

            entity.ToTable("dd_crosssell_item");

            entity.Property(e => e.CrosssellItemId).HasColumnName("crosssell_item_id");
            entity.Property(e => e.Discount)
                .HasPrecision(5)
                .HasColumnName("discount");
            entity.Property(e => e.TargetId).HasColumnName("target_id");
            entity.Property(e => e.Type)
                .HasMaxLength(16)
                .HasColumnName("type");
        });

        modelBuilder.Entity<DdCrosssellSort>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("PRIMARY");

            entity.ToTable("dd_crosssell_sort");

            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
        });

        modelBuilder.Entity<DdCustomer>(entity =>
        {
            entity.HasKey(e => e.CustomerId).HasName("PRIMARY");

            entity.ToTable("dd_customer");

            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.AddressId).HasColumnName("address_id");
            entity.Property(e => e.Cart)
                .HasColumnType("text")
                .HasColumnName("cart");
            entity.Property(e => e.Code)
                .HasMaxLength(40)
                .HasColumnName("code");
            entity.Property(e => e.CustomField)
                .HasColumnType("text")
                .HasColumnName("custom_field");
            entity.Property(e => e.CustomerGroupId).HasColumnName("customer_group_id");
            entity.Property(e => e.DateAdded)
                .HasColumnType("datetime")
                .HasColumnName("date_added");
            entity.Property(e => e.Email)
                .HasMaxLength(96)
                .HasColumnName("email");
            entity.Property(e => e.Fax)
                .HasMaxLength(32)
                .HasColumnName("fax");
            entity.Property(e => e.Firstname)
                .HasMaxLength(32)
                .HasColumnName("firstname");
            entity.Property(e => e.Ip)
                .HasMaxLength(40)
                .HasColumnName("ip");
            entity.Property(e => e.LanguageId).HasColumnName("language_id");
            entity.Property(e => e.Lastname)
                .HasMaxLength(32)
                .HasColumnName("lastname");
            entity.Property(e => e.Newsletter).HasColumnName("newsletter");
            entity.Property(e => e.Password)
                .HasMaxLength(40)
                .HasColumnName("password");
            entity.Property(e => e.Safe).HasColumnName("safe");
            entity.Property(e => e.Salt)
                .HasMaxLength(9)
                .HasColumnName("salt");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.StoreId).HasColumnName("store_id");
            entity.Property(e => e.Telephone)
                .HasMaxLength(32)
                .HasColumnName("telephone");
            entity.Property(e => e.Token)
                .HasColumnType("text")
                .HasColumnName("token");
            entity.Property(e => e.Wishlist)
                .HasColumnType("text")
                .HasColumnName("wishlist");
        });

        modelBuilder.Entity<DdCustomerGroupDescription>(entity =>
        {
            entity.HasKey(e => new { e.CustomerGroupId, e.LanguageId }).HasName("PRIMARY");

            entity.ToTable("dd_customer_group_description");

            entity.Property(e => e.CustomerGroupId).HasColumnName("customer_group_id");
            entity.Property(e => e.LanguageId).HasColumnName("language_id");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.Name)
                .HasMaxLength(32)
                .HasColumnName("name");
        });

        modelBuilder.Entity<DdCustomerHistory>(entity =>
        {
            entity.HasKey(e => e.CustomerHistoryId).HasName("PRIMARY");

            entity.ToTable("dd_customer_history");

            entity.Property(e => e.CustomerHistoryId).HasColumnName("customer_history_id");
            entity.Property(e => e.Comment)
                .HasColumnType("text")
                .HasColumnName("comment");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.DateAdded)
                .HasColumnType("datetime")
                .HasColumnName("date_added");
        });

        modelBuilder.Entity<DdFilter>(entity =>
        {
            entity.HasKey(e => e.FilterId).HasName("PRIMARY");

            entity.ToTable("dd_filter");

            entity.Property(e => e.FilterId).HasColumnName("filter_id");
            entity.Property(e => e.FilterGroupId).HasColumnName("filter_group_id");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
        });

        modelBuilder.Entity<DdFilterDescription>(entity =>
        {
            entity.HasKey(e => new { e.FilterId, e.LanguageId }).HasName("PRIMARY");

            entity.ToTable("dd_filter_description");

            entity.Property(e => e.FilterId).HasColumnName("filter_id");
            entity.Property(e => e.LanguageId).HasColumnName("language_id");
            entity.Property(e => e.FilterGroupId).HasColumnName("filter_group_id");
            entity.Property(e => e.Name)
                .HasMaxLength(64)
                .HasColumnName("name");
        });

        modelBuilder.Entity<DdOptionDescription>(entity =>
        {
            entity.HasKey(e => new { e.OptionId, e.LanguageId }).HasName("PRIMARY");

            entity.ToTable("dd_option_description");

            entity.Property(e => e.OptionId).HasColumnName("option_id");
            entity.Property(e => e.LanguageId).HasColumnName("language_id");
            entity.Property(e => e.Name)
                .HasMaxLength(128)
                .HasColumnName("name");
        });

        modelBuilder.Entity<DdOptionValueDescription>(entity =>
        {
            entity.HasKey(e => new { e.OptionValueId, e.LanguageId }).HasName("PRIMARY");

            entity.ToTable("dd_option_value_description");

            entity.Property(e => e.OptionValueId).HasColumnName("option_value_id");
            entity.Property(e => e.LanguageId).HasColumnName("language_id");
            entity.Property(e => e.Name)
                .HasMaxLength(128)
                .HasColumnName("name");
            entity.Property(e => e.OptionId).HasColumnName("option_id");
        });

        modelBuilder.Entity<DdOrder>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PRIMARY");

            entity.ToTable("dd_order");

            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.AcceptLanguage)
                .HasMaxLength(255)
                .HasColumnName("accept_language");
            entity.Property(e => e.AffiliateId).HasColumnName("affiliate_id");
            entity.Property(e => e.BirthdayDay).HasColumnName("birthday_day");
            entity.Property(e => e.BirthdayMonth).HasColumnName("birthday_month");
            entity.Property(e => e.Callback).HasColumnName("callback");
            entity.Property(e => e.Comment)
                .HasColumnType("text")
                .HasColumnName("comment");
            entity.Property(e => e.Commission)
                .HasPrecision(15, 4)
                .HasColumnName("commission");
            entity.Property(e => e.CurrencyCode)
                .HasMaxLength(3)
                .HasColumnName("currency_code");
            entity.Property(e => e.CurrencyId).HasColumnName("currency_id");
            entity.Property(e => e.CurrencyValue)
                .HasPrecision(15, 8)
                .HasDefaultValueSql("'1.00000000'")
                .HasColumnName("currency_value");
            entity.Property(e => e.CustomField)
                .HasColumnType("text")
                .HasColumnName("custom_field");
            entity.Property(e => e.CustomerGroupId).HasColumnName("customer_group_id");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.DateAdded)
                .HasColumnType("datetime")
                .HasColumnName("date_added");
            entity.Property(e => e.DateModified)
                .HasColumnType("datetime")
                .HasColumnName("date_modified");
            entity.Property(e => e.DateRemind)
                .HasColumnType("datetime")
                .HasColumnName("date_remind");
            entity.Property(e => e.Email)
                .HasMaxLength(96)
                .HasColumnName("email");
            entity.Property(e => e.Fax)
                .HasMaxLength(32)
                .HasColumnName("fax");
            entity.Property(e => e.Firstname)
                .HasMaxLength(32)
                .HasColumnName("firstname");
            entity.Property(e => e.ForwardedIp)
                .HasMaxLength(40)
                .HasColumnName("forwarded_ip");
            entity.Property(e => e.Instagram)
                .HasMaxLength(128)
                .HasDefaultValueSql("''")
                .HasColumnName("instagram");
            entity.Property(e => e.InvoiceNo).HasColumnName("invoice_no");
            entity.Property(e => e.InvoicePrefix)
                .HasMaxLength(26)
                .HasColumnName("invoice_prefix");
            entity.Property(e => e.Ip)
                .HasMaxLength(40)
                .HasColumnName("ip");
            entity.Property(e => e.LanguageId).HasColumnName("language_id");
            entity.Property(e => e.Lastname)
                .HasMaxLength(32)
                .HasColumnName("lastname");
            entity.Property(e => e.MarketingId).HasColumnName("marketing_id");
            entity.Property(e => e.OrderSourceId).HasColumnName("order_source_id");
            entity.Property(e => e.OrderStatusId).HasColumnName("order_status_id");
            entity.Property(e => e.PaymentAddress1)
                .HasMaxLength(128)
                .HasColumnName("payment_address_1");
            entity.Property(e => e.PaymentAddress2)
                .HasMaxLength(128)
                .HasColumnName("payment_address_2");
            entity.Property(e => e.PaymentAddressFormat)
                .HasColumnType("text")
                .HasColumnName("payment_address_format");
            entity.Property(e => e.PaymentCity)
                .HasMaxLength(128)
                .HasColumnName("payment_city");
            entity.Property(e => e.PaymentCode)
                .HasMaxLength(128)
                .HasColumnName("payment_code");
            entity.Property(e => e.PaymentCompany)
                .HasMaxLength(60)
                .HasColumnName("payment_company");
            entity.Property(e => e.PaymentCountry)
                .HasMaxLength(128)
                .HasColumnName("payment_country");
            entity.Property(e => e.PaymentCountryId).HasColumnName("payment_country_id");
            entity.Property(e => e.PaymentCustomField)
                .HasColumnType("text")
                .HasColumnName("payment_custom_field");
            entity.Property(e => e.PaymentFirstname)
                .HasMaxLength(32)
                .HasColumnName("payment_firstname");
            entity.Property(e => e.PaymentLastname)
                .HasMaxLength(32)
                .HasColumnName("payment_lastname");
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(128)
                .HasColumnName("payment_method");
            entity.Property(e => e.PaymentPostcode)
                .HasMaxLength(10)
                .HasColumnName("payment_postcode");
            entity.Property(e => e.PaymentZone)
                .HasMaxLength(128)
                .HasColumnName("payment_zone");
            entity.Property(e => e.PaymentZoneId).HasColumnName("payment_zone_id");
            entity.Property(e => e.Prepayment)
                .HasPrecision(15, 4)
                .HasColumnName("prepayment");
            entity.Property(e => e.ShippingAddress1)
                .HasMaxLength(128)
                .HasColumnName("shipping_address_1");
            entity.Property(e => e.ShippingAddress2)
                .HasMaxLength(128)
                .HasColumnName("shipping_address_2");
            entity.Property(e => e.ShippingAddressFormat)
                .HasColumnType("text")
                .HasColumnName("shipping_address_format");
            entity.Property(e => e.ShippingCity)
                .HasMaxLength(128)
                .HasColumnName("shipping_city");
            entity.Property(e => e.ShippingCode)
                .HasMaxLength(128)
                .HasColumnName("shipping_code");
            entity.Property(e => e.ShippingCompany)
                .HasMaxLength(40)
                .HasColumnName("shipping_company");
            entity.Property(e => e.ShippingCountry)
                .HasMaxLength(128)
                .HasColumnName("shipping_country");
            entity.Property(e => e.ShippingCountryId).HasColumnName("shipping_country_id");
            entity.Property(e => e.ShippingCustomField)
                .HasColumnType("text")
                .HasColumnName("shipping_custom_field");
            entity.Property(e => e.ShippingFirstname)
                .HasMaxLength(32)
                .HasColumnName("shipping_firstname");
            entity.Property(e => e.ShippingFlat)
                .HasMaxLength(32)
                .HasDefaultValueSql("''")
                .HasColumnName("shipping_flat");
            entity.Property(e => e.ShippingHouse)
                .HasMaxLength(32)
                .HasDefaultValueSql("''")
                .HasColumnName("shipping_house");
            entity.Property(e => e.ShippingLastname)
                .HasMaxLength(32)
                .HasColumnName("shipping_lastname");
            entity.Property(e => e.ShippingMethod)
                .HasMaxLength(128)
                .HasColumnName("shipping_method");
            entity.Property(e => e.ShippingPostcode)
                .HasMaxLength(10)
                .HasColumnName("shipping_postcode");
            entity.Property(e => e.ShippingZone)
                .HasMaxLength(128)
                .HasColumnName("shipping_zone");
            entity.Property(e => e.ShippingZoneId).HasColumnName("shipping_zone_id");
            entity.Property(e => e.StoreId).HasColumnName("store_id");
            entity.Property(e => e.StoreName)
                .HasMaxLength(64)
                .HasColumnName("store_name");
            entity.Property(e => e.StoreUrl)
                .HasMaxLength(255)
                .HasColumnName("store_url");
            entity.Property(e => e.Telephone)
                .HasMaxLength(32)
                .HasColumnName("telephone");
            entity.Property(e => e.Total)
                .HasPrecision(15, 4)
                .HasColumnName("total");
            entity.Property(e => e.Tracking)
                .HasMaxLength(64)
                .HasColumnName("tracking");
            entity.Property(e => e.UserAgent)
                .HasMaxLength(255)
                .HasColumnName("user_agent");
            entity.Property(e => e.UserId).HasColumnName("user_id");
        });

        modelBuilder.Entity<DdOrderOption>(entity =>
        {
            entity.HasKey(e => e.OrderOptionId).HasName("PRIMARY");

            entity.ToTable("dd_order_option");

            entity.Property(e => e.OrderOptionId).HasColumnName("order_option_id");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.OrderProductId).HasColumnName("order_product_id");
            entity.Property(e => e.ProductOptionId).HasColumnName("product_option_id");
            entity.Property(e => e.ProductOptionValueId).HasColumnName("product_option_value_id");
            entity.Property(e => e.Type)
                .HasMaxLength(32)
                .HasColumnName("type");
            entity.Property(e => e.Value)
                .HasColumnType("text")
                .HasColumnName("value");
        });

        modelBuilder.Entity<DdOrderProduct>(entity =>
        {
            entity.HasKey(e => e.OrderProductId).HasName("PRIMARY");

            entity.ToTable("dd_order_product");

            entity.HasIndex(e => e.OrderId, "order_id");

            entity.Property(e => e.OrderProductId).HasColumnName("order_product_id");
            entity.Property(e => e.Crosssell).HasColumnName("crosssell");
            entity.Property(e => e.Model)
                .HasMaxLength(64)
                .HasColumnName("model");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.Price)
                .HasPrecision(15, 4)
                .HasColumnName("price");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.Reward).HasColumnName("reward");
            entity.Property(e => e.Tax)
                .HasPrecision(15, 4)
                .HasColumnName("tax");
            entity.Property(e => e.Total)
                .HasPrecision(15, 4)
                .HasColumnName("total");
        });

        modelBuilder.Entity<DdOrderSource>(entity =>
        {
            entity.HasKey(e => new { e.OrderSourceId, e.LanguageId }).HasName("PRIMARY");

            entity.ToTable("dd_order_source");

            entity.Property(e => e.OrderSourceId)
                .ValueGeneratedOnAdd()
                .HasColumnName("order_source_id");
            entity.Property(e => e.LanguageId).HasColumnName("language_id");
            entity.Property(e => e.Name)
                .HasMaxLength(64)
                .HasColumnName("name");
        });

        modelBuilder.Entity<DdOrderStatus>(entity =>
        {
            entity.HasKey(e => new { e.OrderStatusId, e.LanguageId }).HasName("PRIMARY");

            entity.ToTable("dd_order_status");

            entity.Property(e => e.OrderStatusId)
                .ValueGeneratedOnAdd()
                .HasColumnName("order_status_id");
            entity.Property(e => e.LanguageId).HasColumnName("language_id");
            entity.Property(e => e.Color)
                .HasMaxLength(7)
                .HasDefaultValueSql("''")
                .HasColumnName("color");
            entity.Property(e => e.Name)
                .HasMaxLength(32)
                .HasColumnName("name");
        });

        modelBuilder.Entity<DdOrderStatusLog>(entity =>
        {
            entity.HasKey(e => e.OrderStatusLogId).HasName("PRIMARY");

            entity.ToTable("dd_order_status_log");

            entity.HasIndex(e => e.OrderId, "order_id");

            entity.Property(e => e.OrderStatusLogId).HasColumnName("order_status_log_id");
            entity.Property(e => e.DateAdded)
                .HasColumnType("datetime")
                .HasColumnName("date_added");
            entity.Property(e => e.NewStatusId).HasColumnName("new_status_id");
            entity.Property(e => e.OldStatusId).HasColumnName("old_status_id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
        });

        modelBuilder.Entity<DdOrderTotal>(entity =>
        {
            entity.HasKey(e => e.OrderTotalId).HasName("PRIMARY");

            entity.ToTable("dd_order_total");

            entity.HasIndex(e => e.OrderId, "order_id");

            entity.Property(e => e.OrderTotalId).HasColumnName("order_total_id");
            entity.Property(e => e.Code)
                .HasMaxLength(32)
                .HasColumnName("code");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.Value)
                .HasPrecision(15, 4)
                .HasColumnName("value");
        });

        modelBuilder.Entity<DdProduct>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("PRIMARY");

            entity.ToTable("dd_product");

            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.DateAdded)
                .HasColumnType("datetime")
                .HasColumnName("date_added");
            entity.Property(e => e.DateAvailable)
                .HasDefaultValueSql("'0000-00-00'")
                .HasColumnType("date")
                .HasColumnName("date_available");
            entity.Property(e => e.DateModified)
                .HasColumnType("datetime")
                .HasColumnName("date_modified");
            entity.Property(e => e.Ean)
                .HasMaxLength(14)
                .HasColumnName("ean");
            entity.Property(e => e.Height)
                .HasPrecision(15, 8)
                .HasColumnName("height");
            entity.Property(e => e.Image)
                .HasMaxLength(255)
                .HasColumnName("image");
            entity.Property(e => e.Isbn)
                .HasMaxLength(17)
                .HasColumnName("isbn");
            entity.Property(e => e.Jan)
                .HasMaxLength(13)
                .HasColumnName("jan");
            entity.Property(e => e.Length)
                .HasPrecision(15, 8)
                .HasColumnName("length");
            entity.Property(e => e.LengthClassId).HasColumnName("length_class_id");
            entity.Property(e => e.Location)
                .HasMaxLength(128)
                .HasColumnName("location");
            entity.Property(e => e.ManufacturerId).HasColumnName("manufacturer_id");
            entity.Property(e => e.Minimum)
                .HasDefaultValueSql("'1'")
                .HasColumnName("minimum");
            entity.Property(e => e.Model)
                .HasMaxLength(64)
                .HasColumnName("model");
            entity.Property(e => e.Mpn)
                .HasMaxLength(64)
                .HasColumnName("mpn");
            entity.Property(e => e.Points).HasColumnName("points");
            entity.Property(e => e.Price)
                .HasPrecision(15, 4)
                .HasColumnName("price");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.Shipping)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("shipping");
            entity.Property(e => e.Sku)
                .HasMaxLength(64)
                .HasColumnName("sku");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.StockStatusId).HasColumnName("stock_status_id");
            entity.Property(e => e.Subtract)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("subtract");
            entity.Property(e => e.TaxClassId).HasColumnName("tax_class_id");
            entity.Property(e => e.Upc)
                .HasMaxLength(12)
                .HasColumnName("upc");
            entity.Property(e => e.Viewed).HasColumnName("viewed");
            entity.Property(e => e.Weight)
                .HasPrecision(15, 8)
                .HasColumnName("weight");
            entity.Property(e => e.WeightClassId).HasColumnName("weight_class_id");
            entity.Property(e => e.Width)
                .HasPrecision(15, 8)
                .HasColumnName("width");
        });

        modelBuilder.Entity<DdProductDescription>(entity =>
        {
            entity.HasKey(e => new { e.ProductId, e.LanguageId }).HasName("PRIMARY");

            entity.ToTable("dd_product_description");

            entity.HasIndex(e => e.Name, "name");

            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.LanguageId).HasColumnName("language_id");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.MetaDescription)
                .HasMaxLength(255)
                .HasColumnName("meta_description");
            entity.Property(e => e.MetaKeyword)
                .HasMaxLength(255)
                .HasColumnName("meta_keyword");
            entity.Property(e => e.MetaTitle)
                .HasMaxLength(255)
                .HasColumnName("meta_title");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Tag)
                .HasColumnType("text")
                .HasColumnName("tag");
        });

        modelBuilder.Entity<DdProductFilter>(entity =>
        {
            entity.HasKey(e => new { e.ProductId, e.FilterId }).HasName("PRIMARY");

            entity.ToTable("dd_product_filter");

            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.FilterId).HasColumnName("filter_id");
        });

        modelBuilder.Entity<DdProductImage>(entity =>
        {
            entity.HasKey(e => e.ProductImageId).HasName("PRIMARY");

            entity.ToTable("dd_product_image");

            entity.HasIndex(e => e.ProductId, "product_id");

            entity.Property(e => e.ProductImageId).HasColumnName("product_image_id");
            entity.Property(e => e.Image)
                .HasMaxLength(255)
                .HasColumnName("image");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
        });

        modelBuilder.Entity<DdProductLabel>(entity =>
        {
            entity.HasKey(e => e.LabelId).HasName("PRIMARY");

            entity.ToTable("dd_product_label");

            entity.Property(e => e.LabelId).HasColumnName("label_id");
            entity.Property(e => e.BackgroundColor)
                .HasMaxLength(16)
                .HasDefaultValueSql("''")
                .HasColumnName("background_color");
            entity.Property(e => e.Name)
                .HasMaxLength(64)
                .HasColumnName("name");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.Status)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("status");
            entity.Property(e => e.TextColor)
                .HasMaxLength(16)
                .HasDefaultValueSql("''")
                .HasColumnName("text_color");
        });

        modelBuilder.Entity<DdProductManualPin>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("dd_product_manual_pin");

            entity.HasIndex(e => new { e.GroupType, e.GroupKey }, "group");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.GroupKey)
                .HasMaxLength(128)
                .HasColumnName("group_key");
            entity.Property(e => e.GroupType)
                .HasMaxLength(32)
                .HasColumnName("group_type");
            entity.Property(e => e.Position).HasColumnName("position");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
        });

        modelBuilder.Entity<DdProductOption>(entity =>
        {
            entity.HasKey(e => e.ProductOptionId).HasName("PRIMARY");

            entity.ToTable("dd_product_option");

            entity.Property(e => e.ProductOptionId).HasColumnName("product_option_id");
            entity.Property(e => e.OptionId).HasColumnName("option_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Required).HasColumnName("required");
            entity.Property(e => e.Value)
                .HasColumnType("text")
                .HasColumnName("value");
        });

        modelBuilder.Entity<DdProductOptionValue>(entity =>
        {
            entity.HasKey(e => e.ProductOptionValueId).HasName("PRIMARY");

            entity.ToTable("dd_product_option_value");

            entity.Property(e => e.ProductOptionValueId).HasColumnName("product_option_value_id");
            entity.Property(e => e.DateAvailable)
                .HasColumnType("date")
                .HasColumnName("date_available");
            entity.Property(e => e.OptionId).HasColumnName("option_id");
            entity.Property(e => e.OptionValueId).HasColumnName("option_value_id");
            entity.Property(e => e.Points).HasColumnName("points");
            entity.Property(e => e.PointsPrefix)
                .HasMaxLength(1)
                .HasColumnName("points_prefix");
            entity.Property(e => e.PreOrderQuantity).HasColumnName("pre_order_quantity");
            entity.Property(e => e.Price)
                .HasPrecision(15, 4)
                .HasColumnName("price");
            entity.Property(e => e.PricePrefix)
                .HasMaxLength(1)
                .HasColumnName("price_prefix");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.ProductOptionId).HasColumnName("product_option_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.Subtract).HasColumnName("subtract");
            entity.Property(e => e.Weight)
                .HasPrecision(15, 8)
                .HasColumnName("weight");
            entity.Property(e => e.WeightPrefix)
                .HasMaxLength(1)
                .HasColumnName("weight_prefix");
        });

        modelBuilder.Entity<DdProductOrderSnapshot>(entity =>
        {
            entity.HasKey(e => e.SnapshotId).HasName("PRIMARY");

            entity.ToTable("dd_product_order_snapshot");

            entity.Property(e => e.SnapshotId).HasColumnName("snapshot_id");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Note)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("note");
        });

        modelBuilder.Entity<DdProductOrderSnapshotItem>(entity =>
        {
            entity.HasKey(e => new { e.SnapshotId, e.ProductId }).HasName("PRIMARY");

            entity.ToTable("dd_product_order_snapshot_item");

            entity.Property(e => e.SnapshotId).HasColumnName("snapshot_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
        });

        modelBuilder.Entity<DdProductToCategory>(entity =>
        {
            entity.HasKey(e => new { e.ProductId, e.CategoryId }).HasName("PRIMARY");

            entity.ToTable("dd_product_to_category");

            entity.HasIndex(e => e.CategoryId, "category_id");

            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
        });

        modelBuilder.Entity<DdProductToLabel>(entity =>
        {
            entity.HasKey(e => new { e.ProductId, e.LabelId }).HasName("PRIMARY");

            entity.ToTable("dd_product_to_label");

            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.LabelId).HasColumnName("label_id");
        });

        modelBuilder.Entity<DdProductToStore>(entity =>
        {
            entity.HasKey(e => new { e.ProductId, e.StoreId }).HasName("PRIMARY");

            entity.ToTable("dd_product_to_store");

            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.StoreId).HasColumnName("store_id");
        });

        modelBuilder.Entity<DdQuestion>(entity =>
        {
            entity.HasKey(e => e.QuestionId).HasName("PRIMARY");

            entity.ToTable("dd_question");

            entity.Property(e => e.QuestionId).HasColumnName("question_id");
            entity.Property(e => e.DateAdded)
                .HasColumnType("datetime")
                .HasColumnName("date_added");
            entity.Property(e => e.Name)
                .HasMaxLength(64)
                .HasDefaultValueSql("''")
                .HasColumnName("name");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Question)
                .HasColumnType("text")
                .HasColumnName("question");
            entity.Property(e => e.Telephone)
                .HasMaxLength(32)
                .HasDefaultValueSql("''")
                .HasColumnName("telephone");
        });

        modelBuilder.Entity<DdReturn>(entity =>
        {
            entity.HasKey(e => e.ReturnId).HasName("PRIMARY");

            entity.ToTable("dd_return");

            entity.Property(e => e.ReturnId).HasColumnName("return_id");
            entity.Property(e => e.Comment)
                .HasColumnType("text")
                .HasColumnName("comment");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.DateAdded)
                .HasColumnType("datetime")
                .HasColumnName("date_added");
            entity.Property(e => e.DateModified)
                .HasColumnType("datetime")
                .HasColumnName("date_modified");
            entity.Property(e => e.DateOrdered)
                .HasDefaultValueSql("'0000-00-00'")
                .HasColumnType("date")
                .HasColumnName("date_ordered");
            entity.Property(e => e.Email)
                .HasMaxLength(96)
                .HasColumnName("email");
            entity.Property(e => e.Firstname)
                .HasMaxLength(32)
                .HasColumnName("firstname");
            entity.Property(e => e.Lastname)
                .HasMaxLength(32)
                .HasColumnName("lastname");
            entity.Property(e => e.Model)
                .HasMaxLength(64)
                .HasColumnName("model");
            entity.Property(e => e.Opened).HasColumnName("opened");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.Product)
                .HasMaxLength(255)
                .HasColumnName("product");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.ReturnActionId).HasColumnName("return_action_id");
            entity.Property(e => e.ReturnReasonId).HasColumnName("return_reason_id");
            entity.Property(e => e.ReturnStatusId).HasColumnName("return_status_id");
            entity.Property(e => e.Telephone)
                .HasMaxLength(32)
                .HasColumnName("telephone");
        });

        modelBuilder.Entity<DdReturnAction>(entity =>
        {
            entity.HasKey(e => new { e.ReturnActionId, e.LanguageId }).HasName("PRIMARY");

            entity.ToTable("dd_return_action");

            entity.Property(e => e.ReturnActionId)
                .ValueGeneratedOnAdd()
                .HasColumnName("return_action_id");
            entity.Property(e => e.LanguageId).HasColumnName("language_id");
            entity.Property(e => e.Name)
                .HasMaxLength(64)
                .HasColumnName("name");
        });

        modelBuilder.Entity<DdReturnHistory>(entity =>
        {
            entity.HasKey(e => e.ReturnHistoryId).HasName("PRIMARY");

            entity.ToTable("dd_return_history");

            entity.Property(e => e.ReturnHistoryId).HasColumnName("return_history_id");
            entity.Property(e => e.Comment)
                .HasColumnType("text")
                .HasColumnName("comment");
            entity.Property(e => e.DateAdded)
                .HasColumnType("datetime")
                .HasColumnName("date_added");
            entity.Property(e => e.Notify).HasColumnName("notify");
            entity.Property(e => e.ReturnId).HasColumnName("return_id");
            entity.Property(e => e.ReturnStatusId).HasColumnName("return_status_id");
        });

        modelBuilder.Entity<DdReturnReason>(entity =>
        {
            entity.HasKey(e => new { e.ReturnReasonId, e.LanguageId }).HasName("PRIMARY");

            entity.ToTable("dd_return_reason");

            entity.Property(e => e.ReturnReasonId)
                .ValueGeneratedOnAdd()
                .HasColumnName("return_reason_id");
            entity.Property(e => e.LanguageId).HasColumnName("language_id");
            entity.Property(e => e.Name)
                .HasMaxLength(128)
                .HasColumnName("name");
        });

        modelBuilder.Entity<DdReturnStatus>(entity =>
        {
            entity.HasKey(e => new { e.ReturnStatusId, e.LanguageId }).HasName("PRIMARY");

            entity.ToTable("dd_return_status");

            entity.Property(e => e.ReturnStatusId)
                .ValueGeneratedOnAdd()
                .HasColumnName("return_status_id");
            entity.Property(e => e.LanguageId).HasColumnName("language_id");
            entity.Property(e => e.Name)
                .HasMaxLength(32)
                .HasColumnName("name");
        });

        modelBuilder.Entity<DdSetting>(entity =>
        {
            entity.HasKey(e => e.SettingId).HasName("PRIMARY");

            entity.ToTable("dd_setting");

            entity.Property(e => e.SettingId).HasColumnName("setting_id");
            entity.Property(e => e.Code)
                .HasMaxLength(128)
                .HasColumnName("code");
            entity.Property(e => e.Key)
                .HasMaxLength(128)
                .HasColumnName("key");
            entity.Property(e => e.Serialized).HasColumnName("serialized");
            entity.Property(e => e.StoreId).HasColumnName("store_id");
            entity.Property(e => e.Value)
                .HasColumnType("text")
                .HasColumnName("value");
        });

        modelBuilder.Entity<DdSubscribe>(entity =>
        {
            entity.HasKey(e => e.SubscribeId).HasName("PRIMARY");

            entity.ToTable("dd_subscribe");

            entity.Property(e => e.SubscribeId).HasColumnName("subscribe_id");
            entity.Property(e => e.DateAdded)
                .HasColumnType("datetime")
                .HasColumnName("date_added");
            entity.Property(e => e.Email)
                .HasMaxLength(96)
                .HasColumnName("email");
        });

        modelBuilder.Entity<DdUser>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PRIMARY");

            entity.ToTable("dd_user");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Code)
                .HasMaxLength(40)
                .HasColumnName("code");
            entity.Property(e => e.DateAdded)
                .HasColumnType("datetime")
                .HasColumnName("date_added");
            entity.Property(e => e.Email)
                .HasMaxLength(96)
                .HasColumnName("email");
            entity.Property(e => e.Firstname)
                .HasMaxLength(32)
                .HasColumnName("firstname");
            entity.Property(e => e.Image)
                .HasMaxLength(255)
                .HasColumnName("image");
            entity.Property(e => e.Ip)
                .HasMaxLength(40)
                .HasColumnName("ip");
            entity.Property(e => e.Lastname)
                .HasMaxLength(32)
                .HasColumnName("lastname");
            entity.Property(e => e.Password)
                .HasMaxLength(40)
                .HasColumnName("password");
            entity.Property(e => e.Salt)
                .HasMaxLength(9)
                .HasColumnName("salt");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.UserGroupId).HasColumnName("user_group_id");
            entity.Property(e => e.Username)
                .HasMaxLength(20)
                .HasColumnName("username");
        });

        modelBuilder.Entity<ErpOrderManager>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PRIMARY");

            entity.ToTable("erp_order_manager");

            entity.HasIndex(e => e.UserId, "ix_erp_order_manager_user");

            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.AssignedAt)
                .HasColumnType("datetime")
                .HasColumnName("assigned_at");
            entity.Property(e => e.AssignedByUserId).HasColumnName("assigned_by_user_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
