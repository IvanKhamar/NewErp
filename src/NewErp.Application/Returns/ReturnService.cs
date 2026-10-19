using NewErp.Application.Common;
using NewErp.Domain.Common;
using NewErp.Domain.Returns;

namespace NewErp.Application.Returns;

/// <summary>Повернення товарів (dd_return), як «Продажі → Повернення» старого сайту, але з прив'язкою до рядків замовлення.</summary>
public sealed class ReturnService(IReturnStore store, TimeProvider clock)
{
    public Task<PagedResult<ReturnListItem>> ListAsync(ReturnFilter filter, CancellationToken ct)
    {
        var (page, pageSize) = Paging.Normalize(filter.Page, filter.PageSize);
        return store.ListAsync(filter with { Page = page, PageSize = pageSize, Search = filter.Search?.Trim() }, ct);
    }

    public async Task<ReturnDetails> GetAsync(int returnId, CancellationToken ct) =>
        await store.GetAsync(returnId, ct) ?? throw new NotFoundException($"Повернення {returnId} не знайдено.");

    public Task<ReturnLookupsDto> LookupsAsync(CancellationToken ct) => store.LookupsAsync(ct);

    public async Task<int> CreateAsync(CreateReturnRequest request, CancellationToken ct)
    {
        var order = await store.GetSourceOrderAsync(request.OrderId, ct)
            ?? throw new DomainException($"Замовлення {request.OrderId} не знайдено.");
        var lookups = await store.LookupsAsync(ct);
        RequireKnown(lookups.Statuses, request.StatusId, "Оберіть статус повернення.");
        if (request.ReasonId > 0)
            RequireKnown(lookups.Reasons, request.ReasonId, "Такої причини повернення немає.");
        if (request.ActionId != 0)
            RequireKnown(lookups.Actions, request.ActionId, "Такої дії немає.");

        var line = ProductReturn.Validate(
            order.Firstname, order.Lastname, order.Telephone, order.Email, order.Lines, request.ProductId, request.Quantity,
            request.ReasonId, request.Comment, order.Returned.GetValueOrDefault(request.ProductId));
        return await store.CreateAsync(new NewReturnRecord(order, line, request, Now()), ct);
    }

    public async Task AddHistoryAsync(int returnId, AddReturnHistoryRequest request, CancellationToken ct)
    {
        var lookups = await store.LookupsAsync(ct);
        RequireKnown(lookups.Statuses, request.StatusId, "Оберіть статус повернення.");
        if (request.ActionId is { } actionId and not 0)
            RequireKnown(lookups.Actions, actionId, "Такої дії немає.");
        var comment = (request.Comment ?? "").Trim();
        if (comment.Length > ProductReturn.MaxCommentLength)
            throw new DomainException($"Коментар довший за {ProductReturn.MaxCommentLength} символів.");

        if (!await store.AddHistoryAsync(returnId, request.StatusId, comment, request.ActionId, Now(), ct))
            throw new NotFoundException($"Повернення {returnId} не знайдено.");
    }

    private static void RequireKnown(IReadOnlyList<ReturnLookupItem> items, int id, string message)
    {
        if (!items.Any(i => i.Id == id))
            throw new DomainException(message);
    }

    private DateTime Now() => clock.GetLocalNow().DateTime;
}
