using NewErp.Application.Common;
using NewErp.Domain.Common;
using NewErp.Domain.Payments;

namespace NewErp.Application.Payments;

/// <summary>
/// Надходження на рахунки ПриватБанку й зіставлення їх із замовленнями (модуль sale/privatbank старого сайту):
/// імпорт виписки, підказки замовлень, прив'язка й приховування платежів.
/// </summary>
public sealed class BankPaymentService(IBankPaymentRepository repository, IBankStatementClient client, TimeProvider clock, IAuditLog audit)
{
    /// <summary>Скільки замовлень пропонувати для платежу.</summary>
    public const int SuggestionLimit = 10;

    /// <summary>Шукати замовлення, створені не раніше ніж за стільки днів до платежу.</summary>
    public const int SuggestionDays = 60;

    /// <summary>Найдовший період одного імпорту, щоб випадково не тягнути виписку за роки.</summary>
    public const int MaxImportDays = 31;

    /// <summary>Як далеко назад типово дивиться автоматичне зіставлення вже завантажених платежів.</summary>
    public const int AutoMatchDays = 31;

    private const int AutoMatchLimit = 500;

    public Task<PagedResult<BankPaymentDto>> ListAsync(BankPaymentFilter filter, CancellationToken ct) => repository.ListAsync(filter, ct);

    public Task<IReadOnlyList<BankPaymentDto>> ForOrderAsync(int orderId, CancellationToken ct) => repository.ListForOrderAsync(orderId, ct);

    /// <summary>
    /// Завантажує виписку й додає нові надходження. Як і старий сайт, пропускає списання й зарахування від LiqPay
    /// (ті оплати зіставляються з замовленнями самим LiqPay). Без періоду — вчора й сьогодні, як cron старого сайту.
    /// </summary>
    public async Task<BankImportResult> ImportAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        if (!client.IsConfigured)
            return new BankImportResult(false, 0, 0, 0);

        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var end = to ?? today;
        var start = from ?? end.AddDays(-1);
        if (start > end)
            throw new DomainException("Початок періоду пізніше за кінець.");
        if (end.DayNumber - start.DayNumber > MaxImportDays)
            throw new DomainException($"Завантажувати можна не більше ніж {MaxImportDays} днів за раз.");

        var transactions = await client.FetchAsync(start, end, ct);
        var incoming = transactions
            .Where(t => t.IsIncoming && t.Id != "" && !t.Purpose.Contains("LIQPAY", StringComparison.OrdinalIgnoreCase))
            .DistinctBy(t => t.Id)
            .ToList();
        var existing = await repository.ExistingAppCodesAsync(incoming.Select(t => t.Id).ToList(), ct);
        var added = incoming.Where(t => !existing.Contains(t.Id)).ToList();

        var payments = added.Select(t => new BankPayment(t.Id, t.Account, t.Amount, t.Balance, Describe(t), t.Payer, t.Date, t.Time)).ToList();
        foreach (var payment in payments)
            repository.Add(payment);
        await repository.SaveChangesAsync(ct);
        var autoLinked = 0;
        foreach (var payment in payments)
            if (await AutoLinkAsync(payment, ct))
                autoLinked++;
        return new BankImportResult(true, transactions.Count, added.Count, transactions.Count - added.Count, autoLinked);
    }

    /// <summary>
    /// Те саме автоматичне зіставлення для вже завантажених платежів без замовлення (типово за останні AutoMatchDays днів),
    /// наприклад коли замовлення внесли пізніше, ніж прийшли гроші.
    /// </summary>
    public async Task<AutoMatchResult> AutoMatchAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var end = to ?? DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var start = from ?? end.AddDays(-AutoMatchDays);
        if (start > end)
            throw new DomainException("Початок періоду пізніше за кінець.");
        var ids = await repository.UnlinkedIdsAsync(start, end, AutoMatchLimit, ct);
        var links = new List<AutoMatchLink>();
        foreach (var id in ids)
        {
            var payment = await RequireAsync(id, ct);
            if (await AutoLinkAsync(payment, ct))
                links.Add(new AutoMatchLink(payment.Id, payment.OrderId));
        }
        return new AutoMatchResult(ids.Count, links);
    }

    /// <summary>Прив'язує платіж, коли впевнене замовлення рівно одне; зберігає одразу, щоб наступний платіж бачив його зайнятим.</summary>
    private async Task<bool> AutoLinkAsync(BankPayment payment, CancellationToken ct)
    {
        var order = PaymentMatcher.ConfidentMatch(await RankedAsync(payment, null, ct));
        if (order is null)
            return false;
        payment.LinkTo(order.OrderId);
        await repository.SaveChangesAsync(ct);
        audit.Record($"payment auto-linked: {payment.Id}, order {order.OrderId}, amount {payment.Amount}");
        return true;
    }

    private async Task<IReadOnlyList<ScoredCandidate>> RankedAsync(BankPayment payment, string? customer, CancellationToken ct)
    {
        var amounts = new[] { payment.Amount, Math.Floor(payment.Amount) }.Distinct().ToList();
        var query = new CandidateQuery(
            amounts,
            PaymentMatcher.OrderNumbersIn($"{payment.Description} {payment.Terminal}"),
            string.IsNullOrWhiteSpace(customer) ? null : customer.Trim(),
            payment.Timestamp.AddDays(-SuggestionDays),
            payment.Timestamp.AddDays(1),
            SuggestionLimit * 5);
        return PaymentMatcher.Rank(payment, await repository.FindCandidatesAsync(query, ct));
    }

    /// <summary>
    /// Замовлення, яким, імовірно, належить платіж. Кандидати — замовлення з такою ж сумою (повною, завдатком чи залишком),
    /// з номером, згаданим у призначенні, або з клієнтом із пошуку; найімовірніші — першими.
    /// </summary>
    public async Task<IReadOnlyList<PaymentSuggestionDto>> SuggestAsync(int paymentId, string? customer, CancellationToken ct)
    {
        var payment = await RequireAsync(paymentId, ct);
        var ranked = await RankedAsync(payment, customer, ct);

        return ranked
            .Take(SuggestionLimit)
            .Select(s => new PaymentSuggestionDto(
                s.Order.OrderId, s.Order.Customer, s.Order.Telephone, s.Order.Total, s.Order.Prepayment, s.Order.DateAdded,
                s.Order.Status, s.Order.LinkedPaymentId, s.Reasons, s.Score))
            .ToList();
    }

    /// <summary>Прив'язує платіж до замовлення (OrderId = 0 — відв'язує). На одне замовлення може бути кілька платежів (завдаток і доплата).</summary>
    public async Task<BankPaymentDto> LinkAsync(int paymentId, LinkPaymentRequest request, CancellationToken ct)
    {
        var payment = await RequireAsync(paymentId, ct);
        if (request.OrderId == 0)
            payment.Unlink();
        else if (await repository.OrderExistsAsync(request.OrderId, ct))
            payment.LinkTo(request.OrderId);
        else
            throw new NotFoundException($"Замовлення {request.OrderId} не знайдено.");
        await repository.SaveChangesAsync(ct);
        return (await repository.GetDtoAsync(paymentId, ct))!;
    }

    public async Task SetVisibilityAsync(int paymentId, bool visible, CancellationToken ct)
    {
        var payment = await RequireAsync(paymentId, ct);
        if (visible)
            payment.Show();
        else
            payment.Hide();
        await repository.SaveChangesAsync(ct);
    }

    /// <summary>Як старий сайт: призначення платежу, а під ним рахунок, МФО й ЄДРПОУ платника.</summary>
    private static string Describe(BankTransaction t)
    {
        var lines = new List<string> { t.Purpose };
        if (t.PayerAccount != "")
            lines.Add($"Рахунок: {t.PayerAccount}");
        if (t.PayerBankCode != "")
            lines.Add($"МФО: {t.PayerBankCode}");
        if (t.PayerTaxCode != "")
            lines.Add($"ЄДРПОУ: {t.PayerTaxCode}");
        return string.Join("\n", lines.Where(l => l != ""));
    }

    private async Task<BankPayment> RequireAsync(int id, CancellationToken ct) =>
        await repository.GetAsync(id, ct) ?? throw new NotFoundException($"Платіж {id} не знайдено.");
}
