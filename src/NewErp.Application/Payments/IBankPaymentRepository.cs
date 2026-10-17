using NewErp.Application.Common;
using NewErp.Domain.Payments;

namespace NewErp.Application.Payments;

public interface IBankPaymentRepository
{
    Task<PagedResult<BankPaymentDto>> ListAsync(BankPaymentFilter filter, CancellationToken ct);

    Task<BankPayment?> GetAsync(int id, CancellationToken ct);

    Task<BankPaymentDto?> GetDtoAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<BankPaymentDto>> ListForOrderAsync(int orderId, CancellationToken ct);

    /// <summary>Які з цих id транзакцій уже є в базі.</summary>
    Task<IReadOnlySet<string>> ExistingAppCodesAsync(IReadOnlyCollection<string> appCodes, CancellationToken ct);

    Task<IReadOnlyList<OrderCandidate>> FindCandidatesAsync(CandidateQuery query, CancellationToken ct);

    Task<bool> OrderExistsAsync(int orderId, CancellationToken ct);

    void Add(BankPayment payment);

    Task SaveChangesAsync(CancellationToken ct);
}
