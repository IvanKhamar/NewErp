using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using NewErp.Domain.Delivery;

namespace NewErp.Infrastructure.Persistence;

// ТТН Нової Пошти старого сайту: dd_novaposhta_document (структура реконструйована, див. db/reconstructed.sql).
// Колонку api_key (її пише старий модуль International) навмисно не зіставляємо: NewErp ключі з бази не читає.
public partial class ErpDbContext
{
    public DbSet<ShipmentDocument> ShipmentDocuments => Set<ShipmentDocument>();

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
    }
}
