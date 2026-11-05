using Microsoft.Extensions.Time.Testing;

using NewErp.Application.Common;
using NewErp.Application.Payments;
using NewErp.Domain.Common;
using NewErp.Domain.Payments;

namespace NewErp.UnitTests.Application;

public class BankPaymentServiceTests
{
    private readonly FakeBankRepository _repository = new();
    private readonly FakeAudit _audit = new();
    private readonly FakeBankClient _client = new();
    private readonly BankPaymentService _service;
    private static readonly CancellationToken Ct = CancellationToken.None;

    public BankPaymentServiceTests()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _service = new BankPaymentService(_repository, _client, clock, _audit);
    }

    private static BankTransaction Tx(string id, decimal amount, bool incoming = true, string purpose = "Оплата") =>
        new(id, "UA00", new DateOnly(2026, 10, 15), new TimeOnly(10, 0), amount, 5000m, incoming, purpose, "Іваненко І.", "UA11", "305299", "1234567890");

    [Fact]
    public async Task Import_WithoutKeys_DoesNothing()
    {
        _client.IsConfigured = false;

        Assert.Equal(new BankImportResult(false, 0, 0, 0), await _service.ImportAsync(null, null, Ct));
    }

    [Fact]
    public async Task Import_SkipsDebitsLiqPayAndKnown_DefaultsToYesterdayAndToday()
    {
        _repository.Payments.Add(new BankPayment("known", "UA00", 100m, null, "", "", new DateOnly(2026, 10, 15), TimeOnly.MinValue));
        _client.Transactions.AddRange([Tx("a", 1500m), Tx("a", 1500m), Tx("b", 99m, incoming: false), Tx("c", 700m, purpose: "LIQPAY зарахування"), Tx("known", 100m)]);

        var result = await _service.ImportAsync(null, null, Ct);

        Assert.Equal((new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 16)), _client.Period);
        Assert.Equal(new BankImportResult(true, 5, 1, 4), result);
        var added = _repository.Payments.Single(p => p.AppCode == "a");
        Assert.Equal("Оплата\nРахунок: UA11\nМФО: 305299\nЄДРПОУ: 1234567890", added.Description);
        Assert.Equal(("Іваненко І.", 1500m), (added.Terminal, added.Amount));
    }

    [Fact]
    public async Task Import_TooLongPeriod_Refused() =>
        await Assert.ThrowsAsync<DomainException>(() => _service.ImportAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 1), Ct));

    [Fact]
    public async Task Suggest_QueriesByAmountAndNumbers_RanksBestFirst()
    {
        var payment = new BankPayment("p", "UA00", 1500.5m, null, "Оплата зам. 2045", "", new DateOnly(2026, 10, 15), new TimeOnly(10, 0));
        _repository.Payments.Add(payment);
        _repository.Candidates.AddRange([
            new OrderCandidate(2001, "Тарас Клієнт", "", 1500.5m, 0, new DateTime(2026, 10, 14), "2) Прийнято", null),
            new OrderCandidate(2045, "Ірина Коваленко", "", 1450m, 0, new DateTime(2026, 10, 1), "2) Прийнято", null),
        ]);

        var suggestions = await _service.SuggestAsync(payment.Id, " Коваленко ", Ct);

        var query = _repository.LastQuery!;
        Assert.Equal([1500.5m, 1500m], query.Amounts);
        Assert.Equal([2045], query.OrderIds);
        Assert.Equal("Коваленко", query.Customer);
        Assert.Equal([2045, 2001], suggestions.Select(s => s.OrderId));
    }

    [Fact]
    public async Task Link_UnknownOrder_NotFound_ZeroUnlinks()
    {
        var payment = new BankPayment("p", "UA00", 10m, null, "", "", new DateOnly(2026, 10, 15), TimeOnly.MinValue);
        _repository.Payments.Add(payment);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.LinkAsync(payment.Id, new LinkPaymentRequest(999), Ct));
        await _service.LinkAsync(payment.Id, new LinkPaymentRequest(1), Ct);
        Assert.Equal(1, payment.OrderId);
        await _service.LinkAsync(payment.Id, new LinkPaymentRequest(0), Ct);
        Assert.Equal(0, payment.OrderId);
    }

    [Fact]
    public async Task Import_LinksANewPayment_WhenOneOrderIsConfident_AndAuditsIt()
    {
        _repository.Candidates.Add(new OrderCandidate(12345, "Олена", "0670000001", 1500m, 0, new DateTime(2026, 10, 14), "Нове", null));
        _client.Transactions.Add(Tx("tx-new", 1500m, purpose: "Замовлення 12345"));

        var result = await _service.ImportAsync(null, null, Ct);

        Assert.Equal(1, result.AutoLinked);
        var payment = _repository.Payments.Single(p => p.AppCode == "tx-new");
        Assert.Equal(12345, payment.OrderId);
        Assert.Contains("order 12345", Assert.Single(_audit.Lines));
    }

    [Fact]
    public async Task Import_LeavesAPaymentAlone_WhenTwoOrdersAreConfident()
    {
        _repository.Candidates.Add(new OrderCandidate(12345, "Олена", "0670000001", 1500m, 0, new DateTime(2026, 10, 14), "Нове", null));
        _repository.Candidates.Add(new OrderCandidate(12346, "Ірина", "0670000002", 1500m, 0, new DateTime(2026, 10, 14), "Нове", null));
        _client.Transactions.Add(Tx("tx-new", 1500m, purpose: "Замовлення 12345 і 12346"));

        var result = await _service.ImportAsync(null, null, Ct);

        Assert.Equal(0, result.AutoLinked);
        Assert.Equal(0, _repository.Payments.Single(p => p.AppCode == "tx-new").OrderId);
        Assert.Empty(_audit.Lines);
    }

    [Fact]
    public async Task AutoMatch_LaterStartThanEnd_IsRefused() =>
        await Assert.ThrowsAsync<DomainException>(() => _service.AutoMatchAsync(new DateOnly(2026, 10, 31), new DateOnly(2026, 10, 1), Ct));

    private sealed class FakeAudit : NewErp.Application.IAuditLog
    {
        public List<string> Lines { get; } = [];

        public void Record(string text) => Lines.Add(text);
    }

    private sealed class FakeBankClient : IBankStatementClient
    {
        public bool IsConfigured { get; set; } = true;

        public List<BankTransaction> Transactions { get; } = [];

        public (DateOnly From, DateOnly To)? Period { get; private set; }

        public Task<IReadOnlyList<BankTransaction>> FetchAsync(DateOnly from, DateOnly to, CancellationToken ct)
        {
            Period = (from, to);
            return Task.FromResult<IReadOnlyList<BankTransaction>>(Transactions);
        }
    }

    private sealed class FakeBankRepository : IBankPaymentRepository
    {
        public List<BankPayment> Payments { get; } = [];

        public List<OrderCandidate> Candidates { get; } = [];

        public CandidateQuery? LastQuery { get; private set; }

        public Task<PagedResult<BankPaymentDto>> ListAsync(BankPaymentFilter filter, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<int>> UnlinkedIdsAsync(DateOnly from, DateOnly to, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<int>>(Payments.Where(p => p.OrderId == 0).Select(p => p.Id).ToList());

        public Task<BankPayment?> GetAsync(int id, CancellationToken ct) => Task.FromResult(Payments.FirstOrDefault(p => p.Id == id));

        public Task<BankPaymentDto?> GetDtoAsync(int id, CancellationToken ct) =>
            Task.FromResult<BankPaymentDto?>(Payments.Where(p => p.Id == id)
                .Select(p => new BankPaymentDto(p.Id, p.AppCode, p.Card, p.Amount, 0, p.Terminal, p.Description, p.Timestamp, p.OrderId, null, p.IsVisible))
                .FirstOrDefault());

        public Task<IReadOnlyList<BankPaymentDto>> ListForOrderAsync(int orderId, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlySet<string>> ExistingAppCodesAsync(IReadOnlyCollection<string> appCodes, CancellationToken ct) =>
            Task.FromResult<IReadOnlySet<string>>(Payments.Select(p => p.AppCode).Where(appCodes.Contains).ToHashSet());

        public Task<IReadOnlyList<OrderCandidate>> FindCandidatesAsync(CandidateQuery query, CancellationToken ct)
        {
            LastQuery = query;
            return Task.FromResult<IReadOnlyList<OrderCandidate>>(Candidates);
        }

        public Task<bool> OrderExistsAsync(int orderId, CancellationToken ct) => Task.FromResult(orderId == 1);

        public void Add(BankPayment payment) => Payments.Add(payment);

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
