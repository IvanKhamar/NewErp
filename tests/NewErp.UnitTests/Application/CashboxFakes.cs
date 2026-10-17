using NewErp.Application.Cashbox;
using NewErp.Application.Common;
using NewErp.Domain.Cashbox;
using NewErp.Domain.Common;

namespace NewErp.UnitTests.Application;

/// <summary>Підроблений Checkbox: тримає відкриті зміни в пам'яті й запам'ятовує чеки.</summary>
internal sealed class FakeCheckboxClient : ICheckboxClient
{
    public HashSet<int> Configured { get; } = [1, 2];

    public Dictionary<int, string> OpenShifts { get; } = [];

    /// <summary>Зміни, які каса закрила сама (shiftId → id Z-звіту).</summary>
    public Dictionary<string, string> ClosedByCheckbox { get; } = [];

    public List<(int CashierId, FiscalReceipt Receipt)> Sold { get; } = [];

    public string? RejectSale { get; set; }

    public bool CloseIsPending { get; set; }

    private int _next;

    public bool IsFake => false;

    public bool CanUse(int cashierId) => Configured.Contains(cashierId);

    public Task<string> OpenShiftAsync(int cashierId, CancellationToken ct) =>
        Task.FromResult(OpenShifts.TryGetValue(cashierId, out var id) ? id : OpenShifts[cashierId] = $"shift-{++_next}");

    public Task<CheckboxShiftState> GetShiftAsync(int cashierId, string shiftId, CancellationToken ct) =>
        Task.FromResult(ClosedByCheckbox.TryGetValue(shiftId, out var z)
            ? new CheckboxShiftState(shiftId, true, z)
            : new CheckboxShiftState(shiftId, false, null));

    public Task<string?> CloseShiftAsync(int cashierId, CancellationToken ct)
    {
        OpenShifts.Remove(cashierId);
        return Task.FromResult(CloseIsPending ? null : $"z-{++_next}");
    }

    public Task<string> CreateXReportAsync(int cashierId, CancellationToken ct) => Task.FromResult($"x-{++_next}");

    public Task<string> GetReportTextAsync(int cashierId, string reportId, CancellationToken ct) => Task.FromResult($"Звіт {reportId}");

    public Task<string> SellAsync(int cashierId, FiscalReceipt receipt, CancellationToken ct)
    {
        if (RejectSale is not null)
            throw new DomainException(RejectSale);
        Sold.Add((cashierId, receipt));
        return Task.FromResult($"receipt-{++_next}");
    }

    public string? ReceiptUrl(string receiptId) => $"https://checkbox.test/{receiptId}";
}

/// <summary>Сховище каси в пам'яті. Id присвоює, як база, під час SaveChangesAsync.</summary>
internal sealed class FakeCashboxRepository : ICashboxRepository
{
    public List<Cashier> Cashiers { get; } = [];
    public List<CashboxWarehouse> Warehouses { get; } = [];
    public List<CashboxRequisites> RequisitesList { get; } = [];
    public List<WorkShift> Shifts { get; } = [];
    public List<ZReport> Reports { get; } = [];
    public List<Cheque> Cheques { get; } = [];
    public Dictionary<int, OrderForCheque> Orders { get; } = [];
    private readonly List<object> _pending = [];

    public Task<IReadOnlyList<Cashier>> ListCashiersAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Cashier>>(Cashiers);
    public Task<Cashier?> GetCashierAsync(int id, CancellationToken ct) => Task.FromResult(Cashiers.FirstOrDefault(c => c.Id == id));
    public void Add(Cashier cashier) => Track(Cashiers, cashier);
    public Task<IReadOnlyList<CashboxWarehouse>> ListWarehousesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CashboxWarehouse>>(Warehouses);
    public Task<CashboxWarehouse?> GetWarehouseAsync(int id, CancellationToken ct) => Task.FromResult(Warehouses.FirstOrDefault(w => w.Id == id));
    public void Add(CashboxWarehouse warehouse) => Track(Warehouses, warehouse);
    public Task<IReadOnlyList<CashboxRequisites>> ListRequisitesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CashboxRequisites>>(RequisitesList);
    public Task<CashboxRequisites?> GetRequisitesAsync(int id, CancellationToken ct) => Task.FromResult(RequisitesList.FirstOrDefault(r => r.Id == id));
    public void Add(CashboxRequisites requisites) => Track(RequisitesList, requisites);
    public Task<bool> WarehouseInUseAsync(int id, CancellationToken ct) =>
        Task.FromResult(Cashiers.Any(c => c.WarehouseId == id) || Cheques.Any(c => c.WarehouseId == id));
    public Task<bool> RequisitesInUseAsync(int id, CancellationToken ct) =>
        Task.FromResult(Cashiers.Any(c => c.RequisitesId == id) || Cheques.Any(c => c.RequisitesId == id));
    public void Remove(CashboxDirectoryEntry entry)
    {
        Warehouses.Remove(entry as CashboxWarehouse ?? null!);
        RequisitesList.Remove(entry as CashboxRequisites ?? null!);
    }

    public Task<IReadOnlyList<WorkShift>> ListOpenShiftsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<WorkShift>>(Shifts.Where(s => s.IsOpen).ToList());
    public Task<WorkShift?> GetShiftAsync(int id, CancellationToken ct) => Task.FromResult(Shifts.FirstOrDefault(s => s.Id == id));
    public Task<IReadOnlyList<WorkShiftDto>> ListShiftsAsync(WorkShiftFilter filter, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<WorkShiftDto>>(Shifts
            .Where(s => filter.CashierId is null || s.CashierId == filter.CashierId)
            .Select(s => new WorkShiftDto(s.Id, s.CashierId, "", s.CheckboxShiftId, null, s.DateStart, s.DateEnd, 0, 0))
            .ToList());
    public void Add(WorkShift shift) => Track(Shifts, shift);
    public void Add(ZReport report) => Track(Reports, report);
    public Task<IReadOnlyList<ZReportDto>> ListReportsAsync(int? cashierId, DateOnly? from, DateOnly? to, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ZReportDto>>(Reports
            .Where(r => cashierId is null || r.CashierId == cashierId)
            .Select(r => new ZReportDto(r.Id, r.CashierId, "", r.CheckboxReportId, null, r.DateAdded))
            .ToList());
    public Task<IReadOnlyList<Cheque>> ListUnreportedChequesAsync(int cashierId, int shiftId, DateTime since, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Cheque>>(Cheques
            .Where(c => c.CashierId == cashierId && c.ReportId == 0 && (c.WorkShiftId == shiftId || (c.WorkShiftId == 0 && c.DateAdded >= since)))
            .ToList());
    public Task AttachChequesToReportAsync(IReadOnlyList<int> chequeIds, int reportId, CancellationToken ct)
    {
        foreach (var cheque in Cheques.Where(c => chequeIds.Contains(c.Id)))
            typeof(Cheque).GetProperty(nameof(Cheque.ReportId))!.SetValue(cheque, reportId);
        return Task.CompletedTask;
    }
    public Task<Cheque?> GetChequeAsync(int id, CancellationToken ct) => Task.FromResult(Cheques.FirstOrDefault(c => c.Id == id));
    public Task<Cheque?> GetChequeForOrderAsync(int orderId, CancellationToken ct) =>
        Task.FromResult(Cheques.Where(c => c.OrderId == orderId).MaxBy(c => c.Id));
    public Task<ChequeDto?> GetChequeDtoAsync(int id, CancellationToken ct) =>
        Task.FromResult(Cheques.Where(c => c.Id == id).Select(Dto).FirstOrDefault());
    public Task<PagedResult<ChequeDto>> ListChequesAsync(ChequeFilter filter, CancellationToken ct) =>
        Task.FromResult(new PagedResult<ChequeDto>(Cheques.Select(Dto).ToList(), 1, 50, Cheques.Count));
    public void Add(Cheque cheque) => Track(Cheques, cheque);
    public Task DeleteChequeAsync(int id, CancellationToken ct)
    {
        Cheques.RemoveAll(c => c.Id == id);
        return Task.CompletedTask;
    }
    public Task<OrderForCheque?> GetOrderForChequeAsync(int orderId, CancellationToken ct) => Task.FromResult(Orders.GetValueOrDefault(orderId));

    public Task SaveChangesAsync(CancellationToken ct)
    {
        foreach (var entity in _pending)
        {
            // Сетер Id приватний; для успадкованого Id (склад, реквізити) його видно лише з класу, де Id оголошено.
            var property = entity.GetType().GetProperty("Id")!.DeclaringType!.GetProperty("Id")!;
            if ((int)property.GetValue(entity)! == 0)
                property.SetValue(entity, NextId(entity.GetType()));
        }
        _pending.Clear();
        return Task.CompletedTask;
    }

    private readonly Dictionary<Type, int> _ids = [];

    private int NextId(Type type) => _ids[type] = _ids.GetValueOrDefault(type) + 1;

    private void Track<T>(List<T> list, T entity) where T : notnull
    {
        list.Add(entity);
        _pending.Add(entity);
    }

    private static ChequeDto Dto(Cheque c) => new(
        c.Id, c.OrderId, null, c.CashierId, null, null, null, c.Sum, c.CheckboxId, c.IsFiscalized, null, c.WorkShiftId, c.ReportId, c.DateAdded);
}
