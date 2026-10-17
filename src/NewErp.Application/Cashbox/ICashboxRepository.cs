using NewErp.Application.Common;
using NewErp.Domain.Cashbox;

namespace NewErp.Application.Cashbox;

public interface ICashboxRepository
{
    Task<IReadOnlyList<Cashier>> ListCashiersAsync(CancellationToken ct);

    Task<Cashier?> GetCashierAsync(int id, CancellationToken ct);

    void Add(Cashier cashier);

    Task<IReadOnlyList<CashboxWarehouse>> ListWarehousesAsync(CancellationToken ct);

    Task<CashboxWarehouse?> GetWarehouseAsync(int id, CancellationToken ct);

    void Add(CashboxWarehouse warehouse);

    Task<IReadOnlyList<CashboxRequisites>> ListRequisitesAsync(CancellationToken ct);

    Task<CashboxRequisites?> GetRequisitesAsync(int id, CancellationToken ct);

    void Add(CashboxRequisites requisites);

    /// <summary>Чи посилаються на склад (точку) касири чи чеки — тоді видаляти не можна.</summary>
    Task<bool> WarehouseInUseAsync(int id, CancellationToken ct);

    Task<bool> RequisitesInUseAsync(int id, CancellationToken ct);

    void Remove(CashboxDirectoryEntry entry);

    /// <summary>Відкриті зміни (date_end IS NULL) усіх касирів.</summary>
    Task<IReadOnlyList<WorkShift>> ListOpenShiftsAsync(CancellationToken ct);

    Task<WorkShift?> GetShiftAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<WorkShiftDto>> ListShiftsAsync(WorkShiftFilter filter, CancellationToken ct);

    void Add(WorkShift shift);

    void Add(ZReport report);

    Task<IReadOnlyList<ZReportDto>> ListReportsAsync(int? cashierId, DateOnly? from, DateOnly? to, CancellationToken ct);

    /// <summary>Чеки касира, ще не закриті Z-звітом, видані в цю зміну чи (без зміни) після її початку.</summary>
    Task<IReadOnlyList<Cheque>> ListUnreportedChequesAsync(int cashierId, int shiftId, DateTime since, CancellationToken ct);

    /// <summary>Прив'язує чеки до Z-звіту одразу (без SaveChangesAsync), коли id звіту вже відомий.</summary>
    Task AttachChequesToReportAsync(IReadOnlyList<int> chequeIds, int reportId, CancellationToken ct);

    Task<Cheque?> GetChequeAsync(int id, CancellationToken ct);

    Task<Cheque?> GetChequeForOrderAsync(int orderId, CancellationToken ct);

    Task<ChequeDto?> GetChequeDtoAsync(int id, CancellationToken ct);

    Task<PagedResult<ChequeDto>> ListChequesAsync(ChequeFilter filter, CancellationToken ct);

    void Add(Cheque cheque);

    Task DeleteChequeAsync(int id, CancellationToken ct);

    /// <returns>null, якщо замовлення немає.</returns>
    Task<OrderForCheque?> GetOrderForChequeAsync(int orderId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
