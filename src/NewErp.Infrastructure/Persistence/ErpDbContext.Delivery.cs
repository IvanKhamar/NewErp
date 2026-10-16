using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using NewErp.Domain.Delivery;

namespace NewErp.Infrastructure.Persistence;

// ТТН і довідник відділень Нової Пошти старого сайту: dd_novaposhta_document, dd_novaposhta_warehouse (структура
// реконструйована, див. db/reconstructed.sql) і наша erp_shipment_sender.
// Колонку api_key (старий сайт пише туди ключ API відправника) навмисно не зіставляємо: NewErp ключі з бази не читає й не пише.
public partial class ErpDbContext
{
    public DbSet<ShipmentDocument> ShipmentDocuments => Set<ShipmentDocument>();

    public DbSet<NovaPoshtaWarehouse> NovaPoshtaWarehouses => Set<NovaPoshtaWarehouse>();

    public DbSet<ShipmentSenderLink> ShipmentSenderLinks => Set<ShipmentSenderLink>();

    // Провайдер Oracle (MySql.Data) читає колонку date лише як DateTime, тож DateOnly перетворюємо самі.
    // ValueConverter — правило, як EF перекладає значення властивості в значення колонки й назад.
    private static readonly ValueConverter<DateOnly, DateTime> DateOnlyAsDateTime =
        new(d => d.ToDateTime(TimeOnly.MinValue), d => DateOnly.FromDateTime(d));

    private static void ConfigureDelivery(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShipmentDocument>(e =>
        {
            e.ToTable("dd_novaposhta_document");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.Carrier).HasColumnName("carrier").HasMaxLength(32);
            e.Property(x => x.Ref).HasColumnName("ref").HasMaxLength(64);
            e.Property(x => x.Number).HasColumnName("number").HasMaxLength(32);
            e.Property(x => x.Cost).HasColumnName("cost").HasPrecision(15, 4);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(255);
            e.Property(x => x.StatusCode).HasColumnName("status_code");
            e.Property(x => x.RecipientAddress).HasColumnName("recipient_address").HasMaxLength(255);
            e.Property(x => x.Redelivery).HasColumnName("redelivery");
            e.Property(x => x.DateCreated).HasColumnName("date_created").HasColumnType("datetime");
            e.Property(x => x.DateScheduled).HasColumnName("date_scheduled").HasColumnType("date").HasConversion(DateOnlyAsDateTime);
            e.Property(x => x.DatePayedKeeping).HasColumnName("date_payed_keeping").HasColumnType("date").HasConversion(DateOnlyAsDateTime);
            e.Ignore(x => x.IsFinal);
            e.Ignore(x => x.Tone);
        });

        modelBuilder.Entity<NovaPoshtaWarehouse>(e =>
        {
            e.ToTable("dd_novaposhta_warehouse");
            e.HasKey(x => x.Ref);
            e.Property(x => x.Ref).HasColumnName("ref").HasMaxLength(36);
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(255);
            e.Property(x => x.Category).HasColumnName("category_of_warehouse").HasMaxLength(32);
            e.Property(x => x.CityRef).HasColumnName("city_ref").HasMaxLength(36);
            e.Property(x => x.CityDescription).HasColumnName("city_description").HasMaxLength(255);
            e.Property(x => x.MaxWeight).HasColumnName("weight");
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.Property(x => x.Status).HasColumnName("status");
        });

        modelBuilder.Entity<ShipmentSenderLink>(e =>
        {
            e.ToTable("erp_shipment_sender");
            e.HasKey(x => x.ShipmentId);
            e.Property(x => x.ShipmentId).HasColumnName("shipment_id").ValueGeneratedNever();
            e.Property(x => x.SenderId).HasColumnName("sender_id").HasMaxLength(32);
            e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            // Зв'язок лише в моделі EF (зовнішнього ключа в базі немає): так EF підставить id нової ТТН у запис у тому ж SaveChanges.
            e.HasOne(x => x.Shipment).WithOne().HasForeignKey<ShipmentSenderLink>(x => x.ShipmentId);
        });
    }
}

/// <summary>Від якого відправника (id з NovaPoshta:Senders) і хто створив ТТН у NewErp (erp_shipment_sender).</summary>
public class ShipmentSenderLink
{
    public int ShipmentId { get; set; }

    public string SenderId { get; set; } = "";

    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public ShipmentDocument? Shipment { get; set; }
}
