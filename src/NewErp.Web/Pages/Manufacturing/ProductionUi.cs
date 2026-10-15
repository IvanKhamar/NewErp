using NewErp.Application.Production;
using NewErp.Domain.Production;

namespace NewErp.Web.Pages.Manufacturing;

/// <summary>Адреси й підписи виробництва в інтерфейсі.</summary>
public static class ProductionUi
{
    public static string Model(int id) => $"production/models/{id}";

    public static string TypeLabel(string type) => type switch
    {
        ProductionModel.Clothing => "Власний пошив",
        ProductionModel.ReadyMade => "Готовий товар",
        _ => type,
    };

    /// <summary>Назва кнопки операції. Для готового товару «пошито» — це прихід від постачальника.</summary>
    public static string OperationLabel(ProductionOperation operation, string modelType) => operation switch
    {
        ProductionOperation.Cut => "Покроєно",
        ProductionOperation.SendToSewing => "В пошив",
        ProductionOperation.Sewn => modelType == ProductionModel.ReadyMade ? "Прихід" : "Пошито",
        ProductionOperation.Ship => "Відвантажено",
        _ => operation.ToString(),
    };

    /// <summary>Готовий товар не кроять і не шиють: для нього лише прихід і відвантаження.</summary>
    public static IReadOnlyList<ProductionOperation> OperationsFor(string modelType) =>
        modelType == ProductionModel.ReadyMade
            ? [ProductionOperation.Sewn, ProductionOperation.Ship]
            : [ProductionOperation.Cut, ProductionOperation.SendToSewing, ProductionOperation.Sewn, ProductionOperation.Ship];

    public static string LogLabel(ProductionLogDto entry) => (entry.Stage, entry.Direction) switch
    {
        (ProductionLogEntry.Cut, ProductionLogEntry.In) => "Покроєно",
        (ProductionLogEntry.Cut, ProductionLogEntry.Out) => "В пошив",
        (ProductionLogEntry.Sewn, ProductionLogEntry.In) => "Пошито / прихід",
        (ProductionLogEntry.Sewn, ProductionLogEntry.Out) => "Відвантажено / замовлення",
        _ => $"{entry.Stage} {entry.Direction}",
    };
}
