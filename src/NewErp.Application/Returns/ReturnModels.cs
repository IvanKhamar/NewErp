using NewErp.Domain.Returns;

namespace NewErp.Application.Returns;

public sealed record ReturnFilter(int? ReturnId = null, int? OrderId = null, int? StatusId = null, string? Search = null, int Page = 1, int PageSize = 20);

public sealed record ReturnListItem(
    int Id, int OrderId, string Customer, string Telephone, string Product, string Model, int Quantity,
    int StatusId, string? StatusName, string? ActionName, DateTime DateAdded, DateTime DateModified);

public sealed record ReturnHistoryEntry(DateTime DateAdded, int StatusId, string? StatusName, string Comment);

public sealed record ReturnDetails(
    int Id, int OrderId, int ProductId, string Customer, string Telephone, string Email, string Product, string Model, int Quantity,
    bool Opened, int ReasonId, string? ReasonName, int ActionId, string? ActionName, int StatusId, string? StatusName, string Comment,
    DateTime DateAdded, DateTime DateModified, IReadOnlyList<ReturnHistoryEntry> History);

/// <summary>Довідники повернень: статуси, причини й дії (повернено кошти, обмін…).</summary>
public sealed record ReturnLookupsDto(IReadOnlyList<ReturnLookupItem> Statuses, IReadOnlyList<ReturnLookupItem> Reasons, IReadOnlyList<ReturnLookupItem> Actions);

public sealed record ReturnLookupItem(int Id, string Name);

/// <summary>Клієнт і товар беруться із замовлення; ActionId = 0 — дію ще не визначено.</summary>
public sealed record CreateReturnRequest(
    int OrderId, int ProductId, int Quantity, int ReasonId, int StatusId, int ActionId = 0, bool Opened = false, string? Comment = null);

/// <summary>Новий статус повернення з коментарем у журнал і, за потреби, нова дія.</summary>
public sealed record AddReturnHistoryRequest(int StatusId, string? Comment = null, int? ActionId = null);

/// <summary>Замовлення, з якого оформлюють повернення: клієнт і товари з кількістю, яку ще можна повернути.</summary>
public sealed record ReturnSourceOrder(
    int OrderId, string Firstname, string Lastname, string Telephone, string Email, int CustomerId, DateTime DateAdded,
    IReadOnlyList<ReturnableLine> Lines, IReadOnlyDictionary<int, int> Returned);

/// <summary>Те, що треба записати (усі перевірки пройдено).</summary>
public sealed record NewReturnRecord(ReturnSourceOrder Order, ReturnableLine Line, CreateReturnRequest Request, DateTime Now);
