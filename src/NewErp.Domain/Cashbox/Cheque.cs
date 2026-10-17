namespace NewErp.Domain.Cashbox;

/// <summary>Фіскальний чек до замовлення (dd_cashbox_cheque). Один чек на замовлення, як і на старому сайті.</summary>
public class Cheque
{
    private Cheque() { }

    public Cheque(int orderId, Cashier cashier, decimal sum, int workShiftId, DateTime createdAt)
    {
        OrderId = orderId;
        CashierId = cashier.Id;
        WarehouseId = cashier.WarehouseId;
        RequisitesId = cashier.RequisitesId;
        Sum = sum;
        WorkShiftId = workShiftId;
        DateAdded = createdAt;
    }

    public int Id { get; private set; }

    public int OrderId { get; private set; }

    public int CashierId { get; private set; }

    public int WarehouseId { get; private set; }

    public int RequisitesId { get; private set; }

    /// <summary>Сума до оплати: сума замовлення мінус завдаток.</summary>
    public decimal Sum { get; private set; }

    /// <summary>Id чека в Checkbox; порожньо — чек ще не фіскалізовано (Checkbox відмовив або не відповів).</summary>
    public string CheckboxId { get; private set; } = "";

    /// <summary>Зміна, у яку видано чек; 0 — невідомо (чек зі старого сайту).</summary>
    public int WorkShiftId { get; private set; }

    /// <summary>Z-звіт, яким закрито зміну з цим чеком; 0 — зміна ще відкрита.</summary>
    public int ReportId { get; private set; }

    public DateTime DateAdded { get; private set; }

    public bool IsFiscalized => CheckboxId != "";

    public void MarkFiscalized(string checkboxId, int workShiftId)
    {
        CheckboxId = checkboxId;
        WorkShiftId = workShiftId;
    }
}
