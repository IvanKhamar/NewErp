using System.Net;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Catalog;
using NewErp.Domain.Catalog;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.IntegrationTests;

/// <summary>Порядок товарів за продажами. Окрема тестова база, бо тест додає товари, мітку й замовлення.</summary>
public class ProductSortApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const int Category = 30;
    private const string Period = "from=2026-10-01&to=2026-10-10";

    [MySqlFact]
    public async Task Category_SortsBySales_WithPins_AndRestoresSnapshot()
    {
        await AddCatalogAsync();
        var manager = await factory.LoginAsync(ApiFactory.Manager);

        var groups = (await manager.GetFromJsonAsync<List<SortGroupDto>>("/catalog/sort/groups"))!;
        Assert.Contains(new SortGroupDto(SortGroupType.Category, "30", "Сукні"), groups);
        Assert.Contains(new SortGroupDto(SortGroupType.Label, "Хіт", "Мітка: Хіт"), groups);
        Assert.Equal(SortGroupType.CrossSell, groups[^1].Type);

        // Оптове (б2б у коментарі) і створене користувачем 13 замовлення не рахуються.
        var preview = await PreviewAsync(manager);
        Assert.Equal([(202, 3), (201, 1), (203, 0)], preview.Select(r => (r.ProductId, r.Sales)));

        (await manager.PostAsJsonAsync("/catalog/sort/pins", new SetPinRequest(SortGroupType.Category, "30", 1, 203))).EnsureSuccessStatusCode();
        preview = await PreviewAsync(manager);
        Assert.Equal([203, 202, 201], preview.Select(r => r.ProductId));
        Assert.True(preview[0].Pinned);

        var applied = await manager.PostAsJsonAsync("/catalog/sort/apply",
            new ApplySortRequest(SortGroupType.Category, "30", preview.Select(r => r.ProductId).ToList()));
        var result = (await applied.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<SortResult>())!;
        Assert.Equal(3, result.Updated);
        Assert.Equal([(201, 3), (202, 2), (203, 1)], await SortOrdersAsync());

        (await manager.PostAsync($"/catalog/sort/snapshots/{result.SnapshotId}/restore", null)).EnsureSuccessStatusCode();
        Assert.Equal([(201, 0), (202, 0), (203, 0)], await SortOrdersAsync());

        var pin = Assert.Single((await manager.GetFromJsonAsync<List<SortPinDto>>("/catalog/sort/pins"))!);
        Assert.Equal(("Сукня 203", 1), (pin.ProductName, pin.Position));
        (await manager.DeleteAsync($"/catalog/sort/pins/{pin.Id}")).EnsureSuccessStatusCode();

        (await manager.PutAsJsonAsync("/catalog/sort/crosssell-metric", new CrossSellMetricDto(true))).EnsureSuccessStatusCode();
        Assert.True((await manager.GetFromJsonAsync<CrossSellMetricDto>("/catalog/sort/crosssell-metric"))!.PanelOnly);

        var all = await manager.PostAsJsonAsync("/catalog/sort/recalculate-all", new SalesPeriod(null, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 10)));
        Assert.True((await all.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<SortResult>())!.Updated > 0);
        // Мітка йде після категорій, тож її порядок перекриває категорійний: 202 і 201 — перші в мітці.
        Assert.Equal([(201, 2), (202, 1), (203, 3)], await SortOrdersAsync());

        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync("/catalog/sort/groups")).StatusCode);
    }

    private static async Task<List<SortPreviewRow>> PreviewAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<SortPreviewRow>>($"/catalog/sort/preview?type=Category&key=30&{Period}"))!;

    private async Task<List<(int, int)>> SortOrdersAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        return (await db.DdProducts.Where(p => p.ProductId > 200).OrderBy(p => p.ProductId).Select(p => new { p.ProductId, p.SortOrder }).ToListAsync())
            .Select(p => (p.ProductId, p.SortOrder)).ToList();
    }

    private async Task AddCatalogAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        foreach (var id in new[] { 201, 202, 203 })
        {
            db.DdProducts.Add(ApiFactory.Product(id, $"D-{id}", 1000m));
            db.DdProductToCategories.Add(new DdProductToCategory { ProductId = id, CategoryId = Category });
            db.DdProductDescriptions.Add(new DdProductDescription
            {
                ProductId = id, LanguageId = ApiFactory.LanguageId, Name = $"Сукня {id}",
                Description = "", Tag = "", MetaTitle = "", MetaDescription = "", MetaKeyword = "",
            });
        }
        db.DdCategoryDescriptions.Add(new DdCategoryDescription
        {
            CategoryId = Category, LanguageId = ApiFactory.LanguageId, Name = "Сукні",
            Description = "", MetaTitle = "", MetaDescription = "", MetaKeyword = "",
        });
        db.DdProductLabels.Add(new DdProductLabel { LabelId = 1, Name = "Хіт", BackgroundColor = "", TextColor = "", Status = true });
        db.DdProductToLabels.AddRange(new DdProductToLabel { ProductId = 201, LabelId = 1 }, new DdProductToLabel { ProductId = 202, LabelId = 1 });

        var date = new DateTime(2026, 10, 5, 12, 0, 0);
        var retail = ApiFactory.Order(100, ApiFactory.Completed, 4000m, date);
        var wholesale = ApiFactory.Order(101, ApiFactory.Completed, 50000m, date);
        wholesale.Comment = "Замовлення б2б";
        var internalOrder = ApiFactory.Order(102, ApiFactory.Completed, 50000m, date);
        internalOrder.UserId = 13;
        db.DdOrders.AddRange(retail, wholesale, internalOrder);
        db.DdOrderProducts.AddRange(
            Line(100, 100, 201, 1), Line(101, 100, 202, 3), Line(102, 101, 203, 50), Line(103, 102, 203, 50));
        await db.SaveChangesAsync();
    }

    private static DdOrderProduct Line(int id, int orderId, int productId, int quantity) =>
        new() { OrderProductId = id, OrderId = orderId, ProductId = productId, Name = "Сукня", Model = "D", Quantity = quantity, Price = 1000m, Total = 1000m * quantity };
}
