using Microsoft.EntityFrameworkCore;

namespace NewErp.Infrastructure.Persistence;

// Згенерований ErpDbContext.cs викликає цей метод наприкінці OnModelCreating.
// Тут підключаємо вручну описані таблиці erp_*, кожен модуль у своєму файлі.
public partial class ErpDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        ConfigureStock(modelBuilder);
        ConfigureProduction(modelBuilder);
        ConfigureSms(modelBuilder);
        ConfigureDelivery(modelBuilder);
        ConfigureAuth(modelBuilder);
        ConfigureCashbox(modelBuilder);
        ConfigurePartners(modelBuilder);
    }
}
