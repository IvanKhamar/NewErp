using NewErp.Domain.Reports;

namespace NewErp.Application.Reports;

public interface IReportStore
{
    /// <summary>Замовлення з датою в [from; toExclusive): зі статусом statusId або, без нього, з будь-яким оформленим, крім excludedStatusId.</summary>
    Task<IReadOnlyList<SaleFact>> SalesAsync(DateTime from, DateTime toExclusive, int? statusId, int excludedStatusId, CancellationToken ct);

    /// <summary>Товари за кількістю проданого, ті самі умови на замовлення.</summary>
    Task<IReadOnlyList<ProductSalesRow>> ProductsAsync(DateTime from, DateTime toExclusive, int? statusId, int excludedStatusId, int limit, CancellationToken ct);

    Task<int> NewCustomersAsync(DateTime from, DateTime toExclusive, CancellationToken ct);
}
