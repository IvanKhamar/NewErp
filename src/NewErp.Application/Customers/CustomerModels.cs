namespace NewErp.Application.Customers;

public sealed record CustomerFilter(string? Search = null, int? GroupId = null, bool? Status = null, int Page = 1, int PageSize = 20);

public sealed record CustomerListItem(
    int Id, string Name, string Email, string Telephone, string? GroupName, bool Status, DateTime DateAdded, int OrderCount);

public sealed record CustomerGroupDto(int Id, string Name);

public sealed record CustomerAddressDto(int Id, string Name, string City, string Address1, string Address2, bool IsDefault);

public sealed record CustomerOrderItem(int Id, DateTime DateAdded, int StatusId, string? StatusName, decimal Total, string CurrencyCode, bool ByPhone);

public sealed record CustomerNoteDto(DateTime DateAdded, string Comment);

/// <summary>Підсумки за замовленнями; Risk — та сама оцінка, що й у формі нового замовлення.</summary>
public sealed record CustomerStatsDto(
    int OrderCount, int CancelledCount, decimal Spent, DateTime? FirstOrder, DateTime? LastOrder, string RiskLevel, string RiskMessage);

public sealed record CustomerDetails(
    int Id, string Firstname, string Lastname, string Email, string Telephone, string? GroupName, bool Status, bool Newsletter,
    DateTime DateAdded, IReadOnlyList<CustomerAddressDto> Addresses, IReadOnlyList<CustomerOrderItem> Orders,
    CustomerStatsDto Stats, IReadOnlyList<CustomerNoteDto> Notes);

public sealed record AddCustomerNoteRequest(string? Comment);

/// <summary>Покупець без замовлень і нотаток: те, що store віддає сервісу.</summary>
public sealed record CustomerProfile(
    int Id, string Firstname, string Lastname, string Email, string Telephone, string? GroupName, bool Status, bool Newsletter,
    DateTime DateAdded, IReadOnlyList<CustomerAddressDto> Addresses, IReadOnlyList<CustomerNoteDto> Notes);
