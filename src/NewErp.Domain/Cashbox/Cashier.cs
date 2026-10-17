using NewErp.Domain.Common;

namespace NewErp.Domain.Cashbox;

/// <summary>
/// Касир ПРРО Checkbox (dd_cashbox_user старого сайту). Логін, пароль і ключ ліцензії каси сюди не входять:
/// NewErp тримає їх лише в налаштуваннях (dotnet user-secrets), а не в базі.
/// </summary>
public class Cashier
{
    private Cashier() { }

    public Cashier(string name, string description, string paymentCode, TimeOnly? autoCloseTime, int warehouseId, int requisitesId, bool isActive, DateTime createdAt)
    {
        Update(name, description, paymentCode, autoCloseTime, warehouseId, requisitesId, isActive);
        DateAdded = createdAt;
    }

    public int Id { get; private set; }

    /// <summary>Ім'я касира: так воно друкується в чеку.</summary>
    public string Name { get; private set; } = "";

    public string Description { get; private set; } = "";

    /// <summary>Код оплати OpenCart (cod, liqpay...), для замовлень з яким обирається цей касир. Порожньо — касир за замовчуванням.</summary>
    public string PaymentCode { get; private set; } = "";

    /// <summary>О котрій закривати зміну автоматично (Z-звіт). null — не закривати.</summary>
    public TimeOnly? AutoCloseTime { get; private set; }

    public int WarehouseId { get; private set; }

    public int RequisitesId { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime DateAdded { get; private set; }

    public void Update(string name, string description, string paymentCode, TimeOnly? autoCloseTime, int warehouseId, int requisitesId, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Вкажіть ім'я касира: воно друкується в чеку.");
        Name = name.Trim();
        Description = description?.Trim() ?? "";
        PaymentCode = paymentCode?.Trim() ?? "";
        AutoCloseTime = autoCloseTime;
        WarehouseId = warehouseId;
        RequisitesId = requisitesId;
        IsActive = isActive;
    }
}

/// <summary>Довідник каси з назвою й описом: склад (точка продажу) чи реквізити ФОП.</summary>
public abstract class CashboxDirectoryEntry
{
    protected CashboxDirectoryEntry() { }

    protected CashboxDirectoryEntry(string name, string description, DateTime createdAt)
    {
        Rename(name, description);
        DateAdded = createdAt;
    }

    public int Id { get; private set; }

    public string Name { get; private set; } = "";

    public string Description { get; private set; } = "";

    public DateTime DateAdded { get; private set; }

    public void Rename(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Назва обов'язкова.");
        Name = name.Trim();
        Description = description?.Trim() ?? "";
    }
}

/// <summary>Склад або точка продажу каси (dd_cashbox_warehouse).</summary>
public class CashboxWarehouse : CashboxDirectoryEntry
{
    private CashboxWarehouse() { }

    public CashboxWarehouse(string name, string description, DateTime createdAt) : base(name, description, createdAt) { }
}

/// <summary>Реквізити (ФОП), на які видається чек (dd_cashbox_requisites).</summary>
public class CashboxRequisites : CashboxDirectoryEntry
{
    private CashboxRequisites() { }

    public CashboxRequisites(string name, string description, DateTime createdAt) : base(name, description, createdAt) { }
}
