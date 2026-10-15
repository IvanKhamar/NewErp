using Microsoft.EntityFrameworkCore;

namespace NewErp.Infrastructure.Persistence;

public partial class ErpDbContext
{
    public DbSet<ErpUserLogin> UserLogins => Set<ErpUserLogin>();

    private static void ConfigureAuth(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ErpUserLogin>(e =>
        {
            e.ToTable("erp_user_login");
            e.HasKey(x => x.UserId);
            // Id береться з dd_user, база його не генерує.
            e.Property(x => x.UserId).HasColumnName("user_id").ValueGeneratedNever();
            e.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(255);
            e.Property(x => x.Role).HasColumnName("role").HasMaxLength(32);
            e.Property(x => x.LastLoginAt).HasColumnName("last_login_at").HasColumnType("datetime");
            e.Property(x => x.SecurityStamp).HasColumnName("security_stamp").HasMaxLength(36);
            e.Property(x => x.MustChangePassword).HasColumnName("must_change_password");
        });
    }
}
