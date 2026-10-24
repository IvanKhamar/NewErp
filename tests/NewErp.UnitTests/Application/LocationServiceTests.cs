using NewErp.Application.Common;
using NewErp.Application.Locations;
using NewErp.Domain.Locations;

namespace NewErp.UnitTests.Application;

public class LocationServiceTests
{
    private readonly FakeStore _store = new();
    private readonly FakeSpreadsheet _sheet = new();
    private readonly FakeStockLocationRepository _stock = new();
    private readonly LocationService _service;

    public LocationServiceTests()
    {
        _service = new LocationService(_store, _sheet, _stock);
        _store.Items[3] = new ShopLocationData("Старий склад", "", "", "", "Київ", "", "");
    }

    [Fact]
    public async Task Import_UpdatesExistingId_AddsUnknownAndEmptyIds()
    {
        _sheet.Rows = [["ID", "Назва"], ["3", "Склад на Подолі"], ["99", "Пункт у Львові"], ["", "Пункт в Одесі"]];

        var result = await _service.ImportAsync(Stream.Null, default);

        Assert.Equal((2, 1), (result.Added, result.Updated));
        Assert.Equal("Склад на Подолі", _store.Items[3].Name);
        Assert.Equal(["Пункт у Львові", "Пункт в Одесі"], _store.Added.Select(a => a.Name));
    }

    [Fact]
    public async Task Import_WithErrors_WritesNothing()
    {
        _sheet.Rows = [["ID", "Назва"], ["3", "Склад на Подолі"], ["", ""+"", "без назви"]];

        var result = await _service.ImportAsync(Stream.Null, default);

        Assert.Single(result.Errors);
        Assert.Equal("Старий склад", _store.Items[3].Name);
        Assert.Empty(_store.Added);
    }

    [Fact]
    public async Task Update_And_Delete_Unknown_AreNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(8, new SaveLocationRequest("Х"), default));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(8, default));
    }

    [Fact]
    public async Task Delete_WarehouseThatHoldsStock_Rejected()
    {
        _stock.WarehousesWithStock.Add(3);

        await Assert.ThrowsAsync<NewErp.Domain.Common.DomainException>(() => _service.DeleteAsync(3, default));
        Assert.True(_store.Items.ContainsKey(3));
    }

    private sealed class FakeSpreadsheet : ILocationSpreadsheet
    {
        public IReadOnlyList<IReadOnlyList<string>> Rows { get; set; } = [];

        public byte[] Write(IReadOnlyList<string> header, IReadOnlyList<LocationDto> rows) => [];

        public IReadOnlyList<IReadOnlyList<string>> Read(Stream file) => Rows;
    }

    private sealed class FakeStore : ILocationStore
    {
        public Dictionary<int, ShopLocationData> Items { get; } = [];
        public List<ShopLocationData> Added { get; } = [];

        public Task<IReadOnlyList<LocationDto>> ListAsync(string? search, CancellationToken ct) => Task.FromResult<IReadOnlyList<LocationDto>>([]);

        public Task<LocationDto?> GetAsync(int id, CancellationToken ct) => Task.FromResult<LocationDto?>(null);

        public Task<int> AddAsync(ShopLocationData data, CancellationToken ct)
        {
            Added.Add(data);
            return Task.FromResult(100 + Added.Count);
        }

        public Task<bool> UpdateAsync(int id, ShopLocationData data, CancellationToken ct)
        {
            if (!Items.ContainsKey(id))
                return Task.FromResult(false);
            Items[id] = data;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(int id, CancellationToken ct) => Task.FromResult(Items.Remove(id));

        public Task<IReadOnlySet<int>> ExistingIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlySet<int>>(ids.Where(Items.ContainsKey).ToHashSet());
    }
}
