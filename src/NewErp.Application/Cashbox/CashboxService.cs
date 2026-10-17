using NewErp.Application.Common;
using NewErp.Domain.Cashbox;
using NewErp.Domain.Common;

namespace NewErp.Application.Cashbox;

/// <summary>
/// Каса Checkbox (ПРРО), перенесена з модуля cashbox старого сайту: касири, зміни, фіскальні чеки до замовлень,
/// X- і Z-звіти та автоматичне закриття змін.
/// </summary>
public sealed class CashboxService(ICashboxRepository repository, ICheckboxClient client, TimeProvider clock)
{
    /// <summary>Як і старий сайт, щойно відкриту зміну автозакриття не чіпає.</summary>
    public static readonly TimeSpan AutoCloseGrace = TimeSpan.FromMinutes(30);

    private DateTime Now => clock.GetLocalNow().DateTime;

    // ─── Налаштування: касири, склади, реквізити ───

    public async Task<CashboxSetupDto> SetupAsync(CancellationToken ct)
    {
        var cashiers = await repository.ListCashiersAsync(ct);
        var warehouses = await repository.ListWarehousesAsync(ct);
        var requisites = await repository.ListRequisitesAsync(ct);
        var openShifts = (await repository.ListOpenShiftsAsync(ct))
            .GroupBy(s => s.CashierId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.DateStart).First());

        var cashierDtos = cashiers
            .OrderBy(c => c.PaymentCode == "" ? 1 : 0).ThenBy(c => c.Name)
            .Select(c =>
            {
                openShifts.TryGetValue(c.Id, out var shift);
                return new CashierDto(
                    c.Id, c.Name, c.Description, c.PaymentCode, c.AutoCloseTime,
                    c.WarehouseId, warehouses.FirstOrDefault(w => w.Id == c.WarehouseId)?.Name,
                    c.RequisitesId, requisites.FirstOrDefault(r => r.Id == c.RequisitesId)?.Name,
                    c.IsActive, client.CanUse(c.Id), shift?.Id, shift?.DateStart);
            })
            .ToList();

        return new CashboxSetupDto(cashierDtos, Entries(warehouses), Entries(requisites), client.IsFake);
    }

    public async Task<int> CreateCashierAsync(SaveCashierRequest request, CancellationToken ct)
    {
        await CheckReferencesAsync(request, ct);
        var cashier = new Cashier(request.Name, request.Description ?? "", request.PaymentCode ?? "", request.AutoCloseTime,
            request.WarehouseId, request.RequisitesId, request.IsActive, Now);
        repository.Add(cashier);
        await repository.SaveChangesAsync(ct);
        return cashier.Id;
    }

    public async Task UpdateCashierAsync(int id, SaveCashierRequest request, CancellationToken ct)
    {
        var cashier = await RequireCashierAsync(id, ct);
        await CheckReferencesAsync(request, ct);
        cashier.Update(request.Name, request.Description ?? "", request.PaymentCode ?? "", request.AutoCloseTime,
            request.WarehouseId, request.RequisitesId, request.IsActive);
        await repository.SaveChangesAsync(ct);
    }

    public async Task<int> CreateWarehouseAsync(SaveDirectoryEntryRequest request, CancellationToken ct)
    {
        var warehouse = new CashboxWarehouse(request.Name, request.Description ?? "", Now);
        repository.Add(warehouse);
        await repository.SaveChangesAsync(ct);
        return warehouse.Id;
    }

    public async Task UpdateWarehouseAsync(int id, SaveDirectoryEntryRequest request, CancellationToken ct)
    {
        var warehouse = await repository.GetWarehouseAsync(id, ct) ?? throw new NotFoundException($"Склад каси {id} не знайдено.");
        warehouse.Rename(request.Name, request.Description ?? "");
        await repository.SaveChangesAsync(ct);
    }

    /// <summary>Старий сайт видаляв без перевірки, і в касирів та чеків лишалися посилання в нікуди.</summary>
    public async Task DeleteWarehouseAsync(int id, CancellationToken ct)
    {
        var warehouse = await repository.GetWarehouseAsync(id, ct) ?? throw new NotFoundException($"Склад каси {id} не знайдено.");
        if (await repository.WarehouseInUseAsync(id, ct))
            throw new DomainException("Склад указано в касирів чи чеках, видалити його не можна.");
        repository.Remove(warehouse);
        await repository.SaveChangesAsync(ct);
    }

    public async Task<int> CreateRequisitesAsync(SaveDirectoryEntryRequest request, CancellationToken ct)
    {
        var requisites = new CashboxRequisites(request.Name, request.Description ?? "", Now);
        repository.Add(requisites);
        await repository.SaveChangesAsync(ct);
        return requisites.Id;
    }

    public async Task UpdateRequisitesAsync(int id, SaveDirectoryEntryRequest request, CancellationToken ct)
    {
        var requisites = await repository.GetRequisitesAsync(id, ct) ?? throw new NotFoundException($"Реквізити {id} не знайдено.");
        requisites.Rename(request.Name, request.Description ?? "");
        await repository.SaveChangesAsync(ct);
    }

    public async Task DeleteRequisitesAsync(int id, CancellationToken ct)
    {
        var requisites = await repository.GetRequisitesAsync(id, ct) ?? throw new NotFoundException($"Реквізити {id} не знайдено.");
        if (await repository.RequisitesInUseAsync(id, ct))
            throw new DomainException("Реквізити вказано в касирів чи чеках, видалити їх не можна.");
        repository.Remove(requisites);
        await repository.SaveChangesAsync(ct);
    }

    // ─── Зміни й звіти ───

    public Task<IReadOnlyList<WorkShiftDto>> ShiftsAsync(WorkShiftFilter filter, CancellationToken ct) =>
        repository.ListShiftsAsync(filter, ct);

    public Task<IReadOnlyList<ZReportDto>> ReportsAsync(int? cashierId, DateOnly? from, DateOnly? to, CancellationToken ct) =>
        repository.ListReportsAsync(cashierId, from, to, ct);

    /// <summary>
    /// Відкриває зміну касира. Як і старий сайт, якщо в Checkbox зміна вже відкрита (наприклад, з іншого місця),
    /// бере її, а не відкриває другу.
    /// </summary>
    public async Task<WorkShiftDto> OpenShiftAsync(int cashierId, int userId, CancellationToken ct)
    {
        var cashier = await RequireUsableCashierAsync(cashierId, ct);
        if (!cashier.IsActive)
            throw new DomainException($"Касир «{cashier.Name}» вимкнений.");
        if ((await repository.ListOpenShiftsAsync(ct)).Any(s => s.CashierId == cashierId))
            throw new DomainException($"У касира «{cashier.Name}» уже відкрита зміна.");

        var checkboxShiftId = await client.OpenShiftAsync(cashierId, ct);
        var shift = new WorkShift(cashierId, checkboxShiftId, userId, Now);
        repository.Add(shift);
        await repository.SaveChangesAsync(ct);
        return (await repository.ListShiftsAsync(new WorkShiftFilter(cashierId, null, null), ct)).First(s => s.Id == shift.Id);
    }

    /// <summary>Закриває зміну касира в Checkbox (Z-звіт) і в нас.</summary>
    /// <returns>null, якщо Checkbox ще закриває зміну: тоді Z-звіт запише звірка зміни (вручну чи автоматична).</returns>
    public async Task<ZReportDto?> CloseShiftAsync(int cashierId, int userId, CancellationToken ct)
    {
        await RequireUsableCashierAsync(cashierId, ct);
        var shift = (await repository.ListOpenShiftsAsync(ct)).Where(s => s.CashierId == cashierId).MaxBy(s => s.DateStart)
            ?? throw new DomainException("У касира немає відкритої зміни.");

        var zReportId = await client.CloseShiftAsync(cashierId, ct);
        return zReportId is null ? null : await FinalizeAsync(shift, zReportId, userId, ct);
    }

    /// <summary>
    /// Звіряє зміну з Checkbox: якщо каса вже закрила її сама (автоматично опівночі чи з іншого місця), записує Z-звіт у нас.
    /// </summary>
    /// <returns>Z-звіт, якщо зміну закрито; null, якщо вона ще відкрита.</returns>
    public async Task<ZReportDto?> SyncShiftAsync(int shiftId, int userId, CancellationToken ct)
    {
        var shift = await repository.GetShiftAsync(shiftId, ct) ?? throw new NotFoundException($"Зміну {shiftId} не знайдено.");
        if (!shift.IsOpen)
            throw new DomainException("Зміну вже закрито.");
        if (shift.CheckboxShiftId == "")
            throw new DomainException("Зміна не має id у Checkbox, звірити її нема з чим.");
        await RequireUsableCashierAsync(shift.CashierId, ct);

        var state = await client.GetShiftAsync(shift.CashierId, shift.CheckboxShiftId, ct);
        return state is { IsClosed: true, ZReportId: { } zReportId } ? await FinalizeAsync(shift, zReportId, userId, ct) : null;
    }

    /// <summary>
    /// Планова перевірка відкритих змін (на старому сайті — cron): спершу звіряє, чи каса не закрила зміну сама,
    /// потім закриває ті, у яких настав час автозакриття касира. Помилка однієї зміни не зупиняє інші.
    /// </summary>
    public async Task<AutoCloseResult> AutoCloseAsync(CancellationToken ct)
    {
        var now = Now;
        var shifts = (await repository.ListOpenShiftsAsync(ct))
            .Where(s => s.CheckboxShiftId != "" && s.DateStart <= now - AutoCloseGrace && client.CanUse(s.CashierId))
            .ToList();
        int byCheckbox = 0, bySchedule = 0;
        var errors = new List<string>();

        foreach (var shift in shifts)
        {
            try
            {
                if (await SyncShiftAsync(shift.Id, 0, ct) is not null)
                {
                    byCheckbox++;
                    continue;
                }

                var cashier = await repository.GetCashierAsync(shift.CashierId, ct);
                if (cashier?.AutoCloseTime is not { } closeTime)
                    continue;
                var closeAt = DateOnly.FromDateTime(now).ToDateTime(closeTime);
                if (now >= closeAt && shift.DateStart < closeAt)
                {
                    if (await CloseShiftAsync(shift.CashierId, 0, ct) is not null)
                        bySchedule++;
                }
            }
            catch (DomainException e)
            {
                errors.Add($"Зміна {shift.Id}: {e.Message}");
            }
        }

        return new AutoCloseResult(shifts.Count, byCheckbox, bySchedule, errors);
    }

    /// <summary>X-звіт: підсумок поточної зміни без її закриття.</summary>
    public async Task<ReportTextDto> XReportAsync(int cashierId, CancellationToken ct)
    {
        await RequireUsableCashierAsync(cashierId, ct);
        var reportId = await client.CreateXReportAsync(cashierId, ct);
        return new ReportTextDto(reportId, await client.GetReportTextAsync(cashierId, reportId, ct));
    }

    public async Task<ReportTextDto> ZReportTextAsync(int reportId, CancellationToken ct)
    {
        var report = (await repository.ListReportsAsync(null, null, null, ct)).FirstOrDefault(r => r.Id == reportId)
            ?? throw new NotFoundException($"Z-звіт {reportId} не знайдено.");
        await RequireUsableCashierAsync(report.CashierId, ct);
        return new ReportTextDto(report.CheckboxReportId, await client.GetReportTextAsync(report.CashierId, report.CheckboxReportId, ct));
    }

    // ─── Чеки ───

    public async Task<PagedResult<ChequeDto>> ChequesAsync(ChequeFilter filter, CancellationToken ct)
    {
        var page = await repository.ListChequesAsync(filter, ct);
        return page with { Items = page.Items.Select(WithUrl).ToList() };
    }

    public async Task<ChequeDto?> ChequeForOrderAsync(int orderId, CancellationToken ct) =>
        await repository.GetChequeForOrderAsync(orderId, ct) is { } cheque ? await DtoAsync(cheque.Id, ct) : null;

    /// <summary>
    /// Видає фіскальний чек до замовлення (на старому сайті — кнопка «Чек» у замовленні).
    /// Якщо чек уже є, нового не створює: недофіскалізований пробує фіскалізувати ще раз, а фіскалізований повертає як є.
    /// </summary>
    public async Task<ChequeDto> IssueForOrderAsync(int orderId, IssueChequeRequest request, CancellationToken ct)
    {
        var order = await repository.GetOrderForChequeAsync(orderId, ct)
            ?? throw new NotFoundException($"Замовлення {orderId} не знайдено.");

        if (await repository.GetChequeForOrderAsync(orderId, ct) is { } existing)
            return existing.IsFiscalized ? await DtoAsync(existing.Id, ct) : await FiscalizeAsync(existing.Id, ct);

        var cashier = await PickCashierAsync(request.CashierId, order.PaymentCode, ct);
        var shift = await RequireOpenShiftAsync(cashier, ct);
        // Збираємо чек до запису в базу: якщо замовлення не годиться для чека, порожнього запису не лишиться.
        var receipt = FiscalReceipt.Build(orderId, cashier.Name, order.Lines, order.Total - order.Prepayment,
            order.PaymentCode, order.PaymentMethod, order.Email);

        var cheque = new Cheque(orderId, cashier, order.Total - order.Prepayment, shift.Id, Now);
        repository.Add(cheque);
        await repository.SaveChangesAsync(ct);

        // Запис чека вже є, тож якщо Checkbox відмовить, чек лишиться в списку недофіскалізованим і його можна повторити.
        cheque.MarkFiscalized(await client.SellAsync(cashier.Id, receipt, ct), shift.Id);
        await repository.SaveChangesAsync(ct);
        return await DtoAsync(cheque.Id, ct);
    }

    /// <summary>Повторна фіскалізація чека, якого Checkbox не прийняв (на старому сайті — «Створити чек» у списку).</summary>
    public async Task<ChequeDto> FiscalizeAsync(int chequeId, CancellationToken ct)
    {
        var cheque = await repository.GetChequeAsync(chequeId, ct) ?? throw new NotFoundException($"Чек {chequeId} не знайдено.");
        if (cheque.IsFiscalized)
            throw new DomainException("Чек уже фіскалізовано.");
        var order = await repository.GetOrderForChequeAsync(cheque.OrderId, ct)
            ?? throw new DomainException($"Замовлення {cheque.OrderId} чека не знайдено.");
        var cashier = await RequireUsableCashierAsync(cheque.CashierId, ct);
        var shift = await RequireOpenShiftAsync(cashier, ct);

        var receipt = FiscalReceipt.Build(cheque.OrderId, cashier.Name, order.Lines, cheque.Sum,
            order.PaymentCode, order.PaymentMethod, order.Email);
        cheque.MarkFiscalized(await client.SellAsync(cashier.Id, receipt, ct), shift.Id);
        await repository.SaveChangesAsync(ct);
        return await DtoAsync(cheque.Id, ct);
    }

    /// <summary>Видалити можна лише чек, якого немає в Checkbox: фіскалізований чек — уже факт продажу в податковій.</summary>
    public async Task DeleteChequeAsync(int chequeId, CancellationToken ct)
    {
        var cheque = await repository.GetChequeAsync(chequeId, ct) ?? throw new NotFoundException($"Чек {chequeId} не знайдено.");
        if (cheque.IsFiscalized)
            throw new DomainException("Фіскалізований чек видалити не можна: продаж уже зареєстровано в податковій.");
        await repository.DeleteChequeAsync(chequeId, ct);
    }

    private async Task<ZReportDto> FinalizeAsync(WorkShift shift, string zReportId, int userId, CancellationToken ct)
    {
        var now = Now;
        var report = new ZReport(shift.CashierId, zReportId, userId, now);
        repository.Add(report);
        // Закриваємо всі відкриті зміни касира: у Checkbox у касира може бути лише одна, тож решта — завислі записи.
        foreach (var open in (await repository.ListOpenShiftsAsync(ct)).Where(s => s.CashierId == shift.CashierId))
            open.Close(now);
        shift.Close(now);
        await repository.SaveChangesAsync(ct);

        var cheques = await repository.ListUnreportedChequesAsync(shift.CashierId, shift.Id, shift.DateStart, ct);
        await repository.AttachChequesToReportAsync(cheques.Select(c => c.Id).ToList(), report.Id, ct);
        return (await repository.ListReportsAsync(shift.CashierId, null, null, ct)).First(r => r.Id == report.Id);
    }

    /// <summary>
    /// Касир для чека: вказаний явно, або активний касир для коду оплати замовлення, або касир за замовчуванням (без коду).
    /// Старий сайт через помилку завжди шукав касира з порожнім кодом оплати.
    /// </summary>
    private async Task<Cashier> PickCashierAsync(int? cashierId, string paymentCode, CancellationToken ct)
    {
        if (cashierId is { } id)
        {
            var chosen = await RequireUsableCashierAsync(id, ct);
            return chosen.IsActive ? chosen : throw new DomainException($"Касир «{chosen.Name}» вимкнений.");
        }

        var active = (await repository.ListCashiersAsync(ct)).Where(c => c.IsActive).ToList();
        var cashier = active.FirstOrDefault(c => paymentCode != "" && c.PaymentCode == paymentCode)
            ?? active.FirstOrDefault(c => c.PaymentCode == "")
            ?? throw new DomainException(
                $"Немає активного касира для оплати «{paymentCode}» і касира за замовчуванням. Оберіть касира вручну.");
        return client.CanUse(cashier.Id)
            ? cashier
            : throw new DomainException($"Для касира «{cashier.Name}» не задано ключі Checkbox.");
    }

    private async Task<WorkShift> RequireOpenShiftAsync(Cashier cashier, CancellationToken ct) =>
        (await repository.ListOpenShiftsAsync(ct)).Where(s => s.CashierId == cashier.Id).MaxBy(s => s.DateStart)
        ?? throw new DomainException($"У касира «{cashier.Name}» немає відкритої зміни. Спершу відкрийте зміну.");

    private async Task<Cashier> RequireCashierAsync(int id, CancellationToken ct) =>
        await repository.GetCashierAsync(id, ct) ?? throw new NotFoundException($"Касира {id} не знайдено.");

    private async Task<Cashier> RequireUsableCashierAsync(int id, CancellationToken ct)
    {
        var cashier = await RequireCashierAsync(id, ct);
        return client.CanUse(id)
            ? cashier
            : throw new DomainException($"Для касира «{cashier.Name}» не задано ключі Checkbox (Checkbox:Cashiers у user-secrets).");
    }

    private async Task CheckReferencesAsync(SaveCashierRequest request, CancellationToken ct)
    {
        if (request.WarehouseId != 0 && await repository.GetWarehouseAsync(request.WarehouseId, ct) is null)
            throw new DomainException($"Склад каси {request.WarehouseId} не знайдено.");
        if (request.RequisitesId != 0 && await repository.GetRequisitesAsync(request.RequisitesId, ct) is null)
            throw new DomainException($"Реквізити {request.RequisitesId} не знайдено.");
    }

    private async Task<ChequeDto> DtoAsync(int chequeId, CancellationToken ct) =>
        WithUrl((await repository.GetChequeDtoAsync(chequeId, ct))!);

    private ChequeDto WithUrl(ChequeDto dto) => dto.IsFiscalized ? dto with { ReceiptUrl = client.ReceiptUrl(dto.CheckboxId) } : dto;

    private static List<DirectoryEntryDto> Entries(IEnumerable<CashboxDirectoryEntry> entries) =>
        entries.Select(e => new DirectoryEntryDto(e.Id, e.Name, e.Description)).ToList();
}
