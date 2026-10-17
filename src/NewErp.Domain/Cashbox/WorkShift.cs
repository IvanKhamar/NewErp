namespace NewErp.Domain.Cashbox;

/// <summary>Робоча зміна касира (dd_cashbox_work_shift). Чеки можна видавати лише у відкриту зміну.</summary>
public class WorkShift
{
    private WorkShift() { }

    public WorkShift(int cashierId, string checkboxShiftId, int openedByUserId, DateTime openedAt)
    {
        CashierId = cashierId;
        CheckboxShiftId = checkboxShiftId;
        UserId = openedByUserId;
        DateAdded = openedAt;
        DateStart = openedAt;
    }

    public int Id { get; private set; }

    /// <summary>Хто відкрив зміну (dd_user).</summary>
    public int UserId { get; private set; }

    public int CashierId { get; private set; }

    /// <summary>Id зміни в Checkbox.</summary>
    public string CheckboxShiftId { get; private set; } = "";

    public DateTime DateAdded { get; private set; }

    public DateTime DateStart { get; private set; }

    public DateTime? DateEnd { get; private set; }

    public bool IsOpen => DateEnd is null;

    public void Close(DateTime closedAt) => DateEnd ??= closedAt;
}

/// <summary>Z-звіт: зміну закрито (dd_cashbox_report).</summary>
public class ZReport
{
    private ZReport() { }

    public ZReport(int cashierId, string checkboxReportId, int userId, DateTime createdAt)
    {
        CashierId = cashierId;
        CheckboxReportId = checkboxReportId;
        UserId = userId;
        DateAdded = createdAt;
    }

    public int Id { get; private set; }

    /// <summary>Хто закрив зміну; 0 — автоматичне закриття.</summary>
    public int UserId { get; private set; }

    public int CashierId { get; private set; }

    public string CheckboxReportId { get; private set; } = "";

    public DateTime DateAdded { get; private set; }
}
