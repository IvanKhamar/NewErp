using Microsoft.EntityFrameworkCore;

using NewErp.Domain.Sms;

namespace NewErp.Infrastructure.Persistence;

// Журнал SMS старого сайту dd_sms_log (структура реконструйована, див. db/reconstructed.sql).
public partial class ErpDbContext
{
    public DbSet<SmsMessage> SmsMessages => Set<SmsMessage>();

    private static void ConfigureSms(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SmsMessage>(e =>
        {
            e.ToTable("dd_sms_log");
            e.Property(x => x.Id).HasColumnName("message_id");
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.PreOrderId).HasColumnName("pre_order_id");
            e.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(32);
            e.Property(x => x.Text).HasColumnName("text").HasColumnType("text");
            e.Property(x => x.Response).HasColumnName("response").HasColumnType("text");
            e.Property(x => x.SentAt).HasColumnName("date").HasColumnType("datetime");
        });
    }
}
