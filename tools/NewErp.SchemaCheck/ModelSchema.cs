using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using NewErp.Infrastructure.Persistence;

namespace NewErp.SchemaCheck;

/// <summary>
/// Стовпець, якого чекає NewErp. ExplicitType: тип, довжину чи точність задано в моделі (scaffold, HasColumnType), тож тип має
/// збігатися точно; інакше його вибрав провайдер за типом C#, і порівнюємо лише вид (рядок, число, дата…).
/// </summary>
public sealed record ExpectedColumn(string Table, string Column, string StoreType, bool Nullable, bool ExplicitType);

/// <summary>Що NewErp чекає від бази: таблиці й стовпці з моделі EF Core (dd_* зі scaffold, dd_stock_* і erp_* описані вручну).</summary>
public static class ModelSchema
{
    public static IReadOnlyList<ExpectedColumn> Read()
    {
        // Модель будується без підключення до бази: рядок потрібен лише, щоб вибрати провайдер MySQL.
        var options = new DbContextOptionsBuilder<ErpDbContext>().UseMySQL("server=localhost;database=unused").Options;
        using var db = new ErpDbContext(options);
        // Модель часу розробки має всі анотації (типи стовпців), на відміну від стиснутої моделі часу виконання.
        var model = db.GetService<IDesignTimeModel>().Model;

        var columns = new List<ExpectedColumn>();
        foreach (var entity in model.GetEntityTypes())
        {
            if (entity.GetTableName() is not { } table)
                continue;
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            foreach (var property in entity.GetProperties())
            {
                if (property.GetColumnName(store) is not { } column)
                    continue;
                // Scaffold задає рядкам довжину (HasMaxLength), а дробовим — точність (HasPrecision), не сам тип; провайдер
                // тоді будує точний тип (varchar(96), decimal(15,4)), і його теж порівнюємо точно, бо коротший стовпець обріже дані.
                var explicitType = property.FindAnnotation(RelationalAnnotationNames.ColumnType) is not null
                    || property.GetMaxLength() is not null || property.GetPrecision() is not null;
                columns.Add(new ExpectedColumn(table, column, property.GetColumnType(store) ?? property.GetColumnType(), property.IsColumnNullable(store),
                    explicitType));
            }
        }
        // Дві сутності на одну таблицю дали б той самий стовпець двічі.
        return columns.DistinctBy(c => (c.Table.ToLowerInvariant(), c.Column.ToLowerInvariant())).ToList();
    }
}
