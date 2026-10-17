using Microsoft.Extensions.Time.Testing;

using NewErp.Application.Cashbox;
using NewErp.Application.Common;
using NewErp.Domain.Cashbox;
using NewErp.Domain.Common;

namespace NewErp.UnitTests.Application;

public class CashboxServiceTests
{
    private const int UserId = 7;
    private readonly FakeCashboxRepository _repository = new();
    private readonly FakeCheckboxClient _client = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero));
    private readonly CashboxService _service;

    public CashboxServiceTests()
    {
        _clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _service = new CashboxService(_repository, _client, _clock);
        // Касир 1 — за замовчуванням (без коду оплати), касир 2 — для оплати при отриманні, о 21:00 закриває зміну сам.
        _repository.Add(new Cashier("Олена", "", "", null, 0, 0, true, _clock.GetLocalNow().DateTime));
        _repository.Add(new Cashier("Марта (НП)", "", "cod", new TimeOnly(21, 0), 0, 0, true, _clock.GetLocalNow().DateTime));
        _repository.SaveChangesAsync(CancellationToken.None).Wait();
        _repository.Orders[10] = new OrderForCheque(10, 1500m, 300m, "cod", "Оплата при отриманні", "client@test.ua",
            [new ReceiptSourceLine(101, "Сукня", 750m, 2)]);
        _repository.Orders[11] = new OrderForCheque(11, 900m, 0m, "liqpay", "LiqPay", "", [new ReceiptSourceLine(102, "Сорочка", 900m, 1)]);
    }

    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Issue_PicksCashierByPaymentCode_SubtractsPrepayment_AndFiscalizes()
    {
        await _service.OpenShiftAsync(2, UserId, Ct);

        var cheque = await _service.IssueForOrderAsync(10, new IssueChequeRequest(null), Ct);

        Assert.Equal((2, 1200m, true), (cheque.CashierId, cheque.Sum, cheque.IsFiscalized));
        Assert.Equal($"https://checkbox.test/{cheque.CheckboxId}", cheque.ReceiptUrl);
        var (cashierId, receipt) = Assert.Single(_client.Sold);
        Assert.Equal((2, 30000L, ReceiptPaymentType.Cashless), (cashierId, receipt.DiscountKopecks, receipt.Payment.Type));
        Assert.Equal(_repository.Shifts.Single().Id, cheque.WorkShiftId);
    }

    [Fact]
    public async Task Issue_OtherPaymentCode_FallsBackToDefaultCashier_AndSecondCallReturnsSameCheque()
    {
        await _service.OpenShiftAsync(1, UserId, Ct);

        var first = await _service.IssueForOrderAsync(11, new IssueChequeRequest(null), Ct);
        var second = await _service.IssueForOrderAsync(11, new IssueChequeRequest(null), Ct);

        Assert.Equal(1, first.CashierId);
        Assert.Equal(first.Id, second.Id);
        Assert.Single(_client.Sold); // вдруге в Checkbox не надсилається
    }

    [Fact]
    public async Task Issue_WithoutOpenShift_Explains_AndLeavesNoCheque()
    {
        var error = await Assert.ThrowsAsync<DomainException>(() => _service.IssueForOrderAsync(10, new IssueChequeRequest(null), Ct));

        Assert.Contains("відкрийте зміну", error.Message);
        Assert.Empty(_repository.Cheques);
    }

    [Fact]
    public async Task Issue_CheckboxRejects_KeepsChequeForRetry()
    {
        await _service.OpenShiftAsync(2, UserId, Ct);
        _client.RejectSale = "Checkbox відмовив (400): Зміну не відкрито";

        await Assert.ThrowsAsync<DomainException>(() => _service.IssueForOrderAsync(10, new IssueChequeRequest(null), Ct));
        var cheque = Assert.Single(_repository.Cheques);
        Assert.False(cheque.IsFiscalized);

        _client.RejectSale = null;
        var retried = await _service.IssueForOrderAsync(10, new IssueChequeRequest(null), Ct);
        Assert.Equal((cheque.Id, true), (retried.Id, retried.IsFiscalized));
    }

    [Fact]
    public async Task Issue_CashierWithoutKeys_Refused()
    {
        _client.Configured.Remove(2);

        var error = await Assert.ThrowsAsync<DomainException>(() => _service.IssueForOrderAsync(10, new IssueChequeRequest(null), Ct));
        Assert.Contains("ключі Checkbox", error.Message);
    }

    [Fact]
    public async Task OpenShift_Twice_Refused()
    {
        await _service.OpenShiftAsync(1, UserId, Ct);

        await Assert.ThrowsAsync<DomainException>(() => _service.OpenShiftAsync(1, UserId, Ct));
    }

    [Fact]
    public async Task CloseShift_WritesZReport_AndAttachesShiftCheques()
    {
        await _service.OpenShiftAsync(2, UserId, Ct);
        var cheque = await _service.IssueForOrderAsync(10, new IssueChequeRequest(null), Ct);

        var report = await _service.CloseShiftAsync(2, UserId, Ct);

        Assert.NotNull(report);
        Assert.False(_repository.Shifts.Single().IsOpen);
        Assert.Equal(report.Id, _repository.Cheques.Single(c => c.Id == cheque.Id).ReportId);
    }

    [Fact]
    public async Task CloseShift_PendingInCheckbox_KeepsShiftOpenUntilSync()
    {
        await _service.OpenShiftAsync(1, UserId, Ct);
        _client.CloseIsPending = true;

        Assert.Null(await _service.CloseShiftAsync(1, UserId, Ct));
        var shift = _repository.Shifts.Single();
        Assert.True(shift.IsOpen);

        _client.ClosedByCheckbox[shift.CheckboxShiftId] = "z-late";
        var report = await _service.SyncShiftAsync(shift.Id, UserId, Ct);
        Assert.Equal("z-late", report!.CheckboxReportId);
        Assert.False(shift.IsOpen);
    }

    [Fact]
    public async Task AutoClose_ClosesByScheduleAndRecordsShiftsClosedByCheckbox()
    {
        await _service.OpenShiftAsync(1, UserId, Ct); // без розкладу, але каса закрила зміну сама
        await _service.OpenShiftAsync(2, UserId, Ct); // розклад 21:00
        _client.ClosedByCheckbox[_repository.Shifts[0].CheckboxShiftId] = "z-midnight";

        _clock.Advance(TimeSpan.FromMinutes(10)); // щойно відкриті зміни (менше 30 хв) не чіпаємо
        Assert.Equal(0, (await _service.AutoCloseAsync(Ct)).Checked);

        _clock.Advance(TimeSpan.FromHours(12)); // 21:10
        var result = await _service.AutoCloseAsync(Ct);

        Assert.Equal((2, 1, 1), (result.Checked, result.ClosedByCheckbox, result.ClosedBySchedule));
        Assert.All(_repository.Shifts, s => Assert.False(s.IsOpen));
        Assert.Equal(2, _repository.Reports.Count);
    }

    [Fact]
    public async Task DeleteCheque_OnlyIfNotFiscalized()
    {
        await _service.OpenShiftAsync(2, UserId, Ct);
        var cheque = await _service.IssueForOrderAsync(10, new IssueChequeRequest(null), Ct);

        await Assert.ThrowsAsync<DomainException>(() => _service.DeleteChequeAsync(cheque.Id, Ct));
    }

    [Fact]
    public async Task Warehouse_InUse_CannotBeDeleted()
    {
        var warehouseId = await _service.CreateWarehouseAsync(new SaveDirectoryEntryRequest("Магазин", null), Ct);
        await _service.UpdateCashierAsync(1, new SaveCashierRequest("Олена", null, null, null, warehouseId, 0, true), Ct);

        await Assert.ThrowsAsync<DomainException>(() => _service.DeleteWarehouseAsync(warehouseId, Ct));
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.UpdateCashierAsync(1, new SaveCashierRequest("Олена", null, null, null, 999, 0, true), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteWarehouseAsync(999, Ct));
    }

    [Fact]
    public async Task Setup_ShowsKeysAndOpenShift()
    {
        _client.Configured.Remove(2);
        var shift = await _service.OpenShiftAsync(1, UserId, Ct);

        var setup = await _service.SetupAsync(Ct);

        Assert.Equal(["Марта (НП)", "Олена"], setup.Cashiers.Select(c => c.Name)); // спершу касири з кодом оплати
        Assert.Equal((false, (int?)null), (setup.Cashiers[0].CanFiscalize, setup.Cashiers[0].OpenShiftId));
        Assert.Equal((true, (int?)shift.Id), (setup.Cashiers[1].CanFiscalize, setup.Cashiers[1].OpenShiftId));
    }
}
