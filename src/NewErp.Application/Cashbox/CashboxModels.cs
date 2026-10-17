using NewErp.Domain.Cashbox;

namespace NewErp.Application.Cashbox;

/// <summary>
/// Касир для списку. CanFiscalize — для нього задано ключі Checkbox (або працює тестова заглушка).
/// OpenShiftId — відкрита зміна, якщо є.
/// </summary>
public sealed record CashierDto(
    int Id, string Name, string Description, string PaymentCode, TimeOnly? AutoCloseTime,
    int WarehouseId, string? Warehouse, int RequisitesId, string? Requisites, bool IsActive,
    bool CanFiscalize, int? OpenShiftId, DateTime? OpenShiftStart);

public sealed record SaveCashierRequest(
    string Name, string? Description, string? PaymentCode, TimeOnly? AutoCloseTime, int WarehouseId, int RequisitesId, bool IsActive);

public sealed record DirectoryEntryDto(int Id, string Name, string Description);

public sealed record SaveDirectoryEntryRequest(string Name, string? Description);

/// <summary>Усе для налаштування каси одним запитом. FakeMode — ключів немає, працює тестова заглушка Checkbox.</summary>
public sealed record CashboxSetupDto(
    IReadOnlyList<CashierDto> Cashiers, IReadOnlyList<DirectoryEntryDto> Warehouses, IReadOnlyList<DirectoryEntryDto> Requisites, bool FakeMode);

public sealed record WorkShiftDto(
    int Id, int CashierId, string Cashier, string CheckboxShiftId, string? OpenedBy, DateTime DateStart, DateTime? DateEnd, int Cheques, decimal ChequesSum);

public sealed record WorkShiftFilter(int? CashierId, DateOnly? From, DateOnly? To);

public sealed record ZReportDto(int Id, int CashierId, string Cashier, string CheckboxReportId, string? ClosedBy, DateTime DateAdded);

/// <summary>Звіт Checkbox текстом, як його друкує каса.</summary>
public sealed record ReportTextDto(string ReportId, string Text);

/// <summary>Підсумок автоматичного закриття змін: скільки перевірено, скільки закрила сама каса, скільки закрили ми за розкладом.</summary>
public sealed record AutoCloseResult(int Checked, int ClosedByCheckbox, int ClosedBySchedule, IReadOnlyList<string> Errors);

/// <summary>Чек. ReceiptUrl — сторінка чека на сервісі Checkbox (для друку чи надсилання клієнту).</summary>
public sealed record ChequeDto(
    int Id, int OrderId, string? Customer, int CashierId, string? Cashier, string? Warehouse, string? Requisites,
    decimal Sum, string CheckboxId, bool IsFiscalized, string? ReceiptUrl, int WorkShiftId, int ReportId, DateTime DateAdded);

public sealed record ChequeFilter(int? CashierId, int? OrderId, DateOnly? From, DateOnly? To, bool? Fiscalized, int Page = 1, int PageSize = 50);

/// <param name="CashierId">Якого касира взяти; null — за кодом оплати замовлення, як на старому сайті.</param>
public sealed record IssueChequeRequest(int? CashierId);

/// <summary>Що потрібно з замовлення для чека.</summary>
public sealed record OrderForCheque(
    int OrderId, decimal Total, decimal Prepayment, string PaymentCode, string PaymentMethod, string Email,
    IReadOnlyList<ReceiptSourceLine> Lines);

/// <summary>Стан зміни на боці Checkbox. ZReportId — якщо зміну вже закрито (наприклад, автоматично самою касою).</summary>
public sealed record CheckboxShiftState(string ShiftId, bool IsClosed, string? ZReportId);
