using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using NewErp.Domain.Cashbox;
using NewErp.Domain.Payments;

namespace NewErp.Infrastructure.Persistence;

// Каса Checkbox і надходження ПриватБанку старого сайту: dd_cashbox_* і dd_privatbank (структура реконструйована,
// див. db/reconstructed.sql). Колонки з ключами касирів (login, password, checkbox_key, access_token) навмисно не зіставляємо:
// NewErp їх не читає й не пише, ключі лише в dotnet user-secrets.
public partial class ErpDbContext
{
    public DbSet<Cashier> Cashiers => Set<Cashier>();

    public DbSet<CashboxWarehouse> CashboxWarehouses => Set<CashboxWarehouse>();

    public DbSet<CashboxRequisites> CashboxRequisites => Set<CashboxRequisites>();

    public DbSet<WorkShift> WorkShifts => Set<WorkShift>();

    public DbSet<Cheque> Cheques => Set<Cheque>();

    public DbSet<ZReport> ZReports => Set<ZReport>();

    public DbSet<BankPayment> BankPayments => Set<BankPayment>();

    // Колонку time провайдер читає як TimeSpan, тож TimeOnly перетворюємо самі (як DateOnly в ErpDbContext.Delivery.cs).
    private static readonly ValueConverter<TimeOnly, TimeSpan> TimeOnlyAsTimeSpan =
        new(t => t.ToTimeSpan(), t => TimeOnly.FromTimeSpan(t));

    private static void ConfigureCashbox(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cashier>(e =>
        {
            e.ToTable("dd_cashbox_user");
            e.Property(x => x.Id).HasColumnName("user_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(255);
            e.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
            e.Property(x => x.PaymentCode).HasColumnName("payment_code").HasMaxLength(128);
            e.Property(x => x.AutoCloseTime).HasColumnName("auto_close_time").HasColumnType("time").HasConversion(TimeOnlyAsTimeSpan);
            e.Property(x => x.WarehouseId).HasColumnName("warehouse_id");
            e.Property(x => x.RequisitesId).HasColumnName("requisites_id");
            e.Property(x => x.IsActive).HasColumnName("status");
            e.Property(x => x.DateAdded).HasColumnName("date_added").HasColumnType("datetime");
        });

        // Склад і реквізити — дві окремі таблиці однакової будови. Спільний базовий клас CashboxDirectoryEntry є лише в C#:
        // у модель EF він не входить, тож це не ієрархія успадкування, а дві незалежні сутності.
        modelBuilder.Entity<CashboxWarehouse>(e =>
        {
            e.ToTable("dd_cashbox_warehouse");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("warehouse_id");
            DirectoryColumns(e);
        });

        modelBuilder.Entity<CashboxRequisites>(e =>
        {
            e.ToTable("dd_cashbox_requisites");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("requisites_id");
            DirectoryColumns(e);
        });

        modelBuilder.Entity<WorkShift>(e =>
        {
            e.ToTable("dd_cashbox_work_shift");
            e.Property(x => x.Id).HasColumnName("work_shift_id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.CashierId).HasColumnName("cashbox_user_id");
            e.Property(x => x.CheckboxShiftId).HasColumnName("checkbox_shift_id").HasMaxLength(64);
            e.Property(x => x.DateAdded).HasColumnName("date_added").HasColumnType("datetime");
            e.Property(x => x.DateStart).HasColumnName("date_start").HasColumnType("datetime");
            e.Property(x => x.DateEnd).HasColumnName("date_end").HasColumnType("datetime");
            e.Ignore(x => x.IsOpen);
        });

        modelBuilder.Entity<Cheque>(e =>
        {
            e.ToTable("dd_cashbox_cheque");
            e.Property(x => x.Id).HasColumnName("cheque_id");
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.CashierId).HasColumnName("cashbox_user_id");
            e.Property(x => x.WarehouseId).HasColumnName("cashbox_warehouse_id");
            e.Property(x => x.RequisitesId).HasColumnName("cashbox_requisites_id");
            e.Property(x => x.Sum).HasColumnName("sum").HasPrecision(15, 4);
            e.Property(x => x.CheckboxId).HasColumnName("checkbox_id").HasMaxLength(64);
            e.Property(x => x.WorkShiftId).HasColumnName("cashbox_work_shift_id");
            e.Property(x => x.ReportId).HasColumnName("cashbox_report_id");
            e.Property(x => x.DateAdded).HasColumnName("date_added").HasColumnType("datetime");
            e.Ignore(x => x.IsFiscalized);
        });

        modelBuilder.Entity<ZReport>(e =>
        {
            e.ToTable("dd_cashbox_report");
            e.Property(x => x.Id).HasColumnName("report_id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.CashierId).HasColumnName("cashbox_user_id");
            e.Property(x => x.CheckboxReportId).HasColumnName("z_report_id").HasMaxLength(64);
            e.Property(x => x.DateAdded).HasColumnName("date_added").HasColumnType("datetime");
        });

        modelBuilder.Entity<BankPayment>(e =>
        {
            e.ToTable("dd_privatbank");
            e.Property(x => x.Id).HasColumnName("payment_id");
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.AppCode).HasColumnName("appcode").HasMaxLength(64);
            e.Property(x => x.Card).HasColumnName("card").HasMaxLength(64);
            e.Property(x => x.AmountText).HasColumnName("amount").HasMaxLength(32);
            e.Property(x => x.CardAmountText).HasColumnName("cardamount").HasMaxLength(32);
            e.Property(x => x.RestText).HasColumnName("rest").HasMaxLength(32);
            e.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
            e.Property(x => x.Terminal).HasColumnName("terminal").HasMaxLength(255);
            e.Property(x => x.TranDate).HasColumnName("trandate").HasColumnType("date").HasConversion(DateOnlyAsDateTime);
            e.Property(x => x.TranTime).HasColumnName("trantime").HasColumnType("time").HasConversion(TimeOnlyAsTimeSpan);
            e.Property(x => x.IsVisible).HasColumnName("show_status");
            e.Ignore(x => x.Amount);
            e.Ignore(x => x.Timestamp);
        });
    }

    private static void DirectoryColumns<T>(EntityTypeBuilder<T> e)
        where T : CashboxDirectoryEntry
    {
        e.Property(x => x.Name).HasColumnName("name").HasMaxLength(255);
        e.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        e.Property(x => x.DateAdded).HasColumnName("date_added").HasColumnType("datetime");
    }
}
