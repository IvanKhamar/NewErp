using System.Globalization;

using Microsoft.Extensions.Options;

using NewErp.Application.Common;
using NewErp.Application.Sms;
using NewErp.Domain.Common;
using NewErp.Domain.Marketing;
using NewErp.Domain.Sms;

namespace NewErp.Application.Marketing;

/// <summary>
/// Подарункові сертифікати, як «Продажі → Подарункові сертифікати» старого сайту, у таблицях самого OpenCart, щоб сайт і далі їх
/// приймав. Стара адмінка надсилала сертифікат листом; тут — SMS на телефон, який вводять під час надсилання. Як у Java-версії.
/// </summary>
public sealed class VoucherService(IVoucherStore store, ISmsSender sender, ISmsLog smsLog, IOptions<VoucherOptions> options, TimeProvider clock)
{
    private const int CodeAttempts = 20;
    private readonly VoucherOptions _options = options.Value;

    public async Task<IReadOnlyList<VoucherCardDto>> ListAsync(string? search, CancellationToken ct) =>
        (await store.ListAsync(search?.Trim() ?? "", ct)).Select(r => Card(r, [])).ToList();

    public async Task<VoucherCardDto> GetAsync(int id, CancellationToken ct) => Card(await RequireAsync(id, ct), await store.HistoryAsync(id, ct));

    public Task<IReadOnlyList<LookupItem>> ThemesAsync(CancellationToken ct) => store.ThemesAsync(ct);

    public async Task<VoucherCardDto> CreateAsync(SaveVoucherRequest request, CancellationToken ct)
    {
        var code = string.IsNullOrWhiteSpace(request.Code) ? await FreeCodeAsync(ct) : request.Code;
        var voucher = await ValidateAsync(request, code, null, ct);
        return await GetAsync(await store.AddAsync(voucher, clock.GetLocalNow().DateTime, ct), ct);
    }

    /// <summary>Суму можна змінити й після списань (залишок іде за нею); історія лишається.</summary>
    public async Task<VoucherCardDto> UpdateAsync(int id, SaveVoucherRequest request, CancellationToken ct)
    {
        await RequireAsync(id, ct);
        await store.UpdateAsync(id, await ValidateAsync(request, request.Code, id, ct), ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Лише сертифікат, яким не платили: старий сайт видаляв із ним історію, а з нею й облік грошей.</summary>
    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        await RequireAsync(id, ct);
        if ((await store.HistoryAsync(id, ct)).Count > 0)
            throw new DomainException("Сертифікатом уже платили. Вимкніть його замість видалення, щоб лишилась історія.");
        await store.DeleteAsync(id, ct);
    }

    public async Task<SmsSendResult> SendSmsAsync(int id, string? phone, CancellationToken ct)
    {
        var voucher = await GetAsync(id, ct);
        if (!voucher.Usable || voucher.Balance <= 0)
            throw new DomainException("Сертифікат зараз не можна використати (вимкнений, не оплачений або витрачений), тож SMS не надсилаємо.");

        var number = SmsMessage.NormalizePhone(phone);
        var text = SmsMessage.RequireText(_options.SmsText
            .Replace("{amount}", voucher.Balance.ToString("0.##", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{code}", voucher.Code, StringComparison.Ordinal));
        var result = await sender.SendAsync(number, text, ct);
        smsLog.Add(new SmsMessage(voucher.OrderId, number, text, result.Response, clock.GetLocalNow().DateTime));
        await smsLog.SaveChangesAsync(ct);
        return result;
    }

    private async Task<GiftVoucher> ValidateAsync(SaveVoucherRequest r, string? code, int? exceptId, CancellationToken ct)
    {
        var voucher = GiftVoucher.Of(code, r.FromName, r.FromEmail, r.ToName, r.ToEmail, r.ThemeId, r.Message, r.Amount, r.Active);
        if (await store.CodeTakenAsync(voucher.Code, exceptId, ct))
            throw new DomainException($"Сертифікат з кодом {voucher.Code} уже є.");
        return voucher;
    }

    private async Task<string> FreeCodeAsync(CancellationToken ct)
    {
        for (var i = 0; i < CodeAttempts; i++)
        {
            var code = GiftVoucher.NewCode(Random.Shared);
            if (!await store.CodeTakenAsync(code, null, ct))
                return code;
        }
        throw new InvalidOperationException("Не вдалося підібрати вільний код сертифіката.");
    }

    private async Task<VoucherRow> RequireAsync(int id, CancellationToken ct) =>
        await store.FindAsync(id, ct) ?? throw new NotFoundException($"Сертифікат {id} не знайдено.");

    private VoucherCardDto Card(VoucherRow r, IReadOnlyList<VoucherHistoryRow> history)
    {
        var paid = r.OrderStatusId is { } status && _options.PaidStatusIds.Contains(status);
        return new VoucherCardDto(r.Id, r.OrderId, r.Code, r.FromName, r.FromEmail, r.ToName, r.ToEmail, r.ThemeId, r.ThemeName, r.Message,
            r.Amount, GiftVoucher.Balance(r.Amount, r.Spent), r.Active, GiftVoucher.Usable(r.Active, r.OrderId, paid), r.CreatedAt, history);
    }
}
