using System.Net;
using System.Net.Http.Json;

using NewErp.Application.Orders;
using NewErp.Application.Production;

namespace NewErp.IntegrationTests;

/// <summary>Зміна складу замовлення. Окрема тестова база, бо тест змінює товари й статус замовлення CategoryOrderId.</summary>
public class OrderCompositionApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const int SizeM = 502; // product_option_value розміру M товару CategoryProductId

    private sealed record Created(int Id);

    [MySqlFact]
    public async Task NewComposition_ReplacesLinesAndTotals_AndRecountsSewnStock()
    {
        var admin = await factory.LoginAsync(ApiFactory.Admin);
        var created = await admin.PostAsJsonAsync("/production/models", new SaveProductionModelRequest("Сорочка", [new("M")]));
        var modelId = (await created.Content.ReadFromJsonAsync<Created>())!.Id;
        var sizeM = (await admin.GetFromJsonAsync<ProductionModelDto>($"/production/models/{modelId}"))!.Sizes[0].Id;
        (await admin.PutAsJsonAsync($"/production/models/{modelId}/links",
            new SetProductionLinksRequest([ApiFactory.CategoryProductId], [], [new(sizeM, ApiFactory.SizeMOptionValueId)]))).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/production/models/{modelId}/sewn", new ProductionOperationRequest([new(sizeM, 5)]))).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/orders/{ApiFactory.CategoryOrderId}/status", new ChangeOrderStatusRequest(ApiFactory.Accepted))).EnsureSuccessStatusCode();
        Assert.Equal(4, await SewnAsync()); // прийняте замовлення забрало 1

        var manager = await factory.LoginAsync(ApiFactory.Manager);
        var current = await manager.GetFromJsonAsync<OrderCompositionDto>($"/orders/{ApiFactory.CategoryOrderId}/composition");
        var line = Assert.Single(current!.Lines);
        Assert.Equal((ApiFactory.CategoryProductId, (int?)SizeM, 1, 900m), (line.ProductId, line.ProductOptionValueId, line.Quantity, line.Price));

        // Клієнт хоче 3 сорочки й платну доставку: замовлення повертає 1 і бере 3.
        var response = await manager.PutAsJsonAsync($"/orders/{ApiFactory.CategoryOrderId}/composition",
            new UpdateOrderCompositionRequest([new(ApiFactory.CategoryProductId, SizeM, 3, 900)], ShippingCost: 70, Discount: 100, Prepayment: 500));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<UpdateOrderCompositionResult>();
        Assert.Equal(2670m, result!.Total);
        Assert.Empty(result.Warnings);
        Assert.Equal(2, await SewnAsync());

        current = await manager.GetFromJsonAsync<OrderCompositionDto>($"/orders/{ApiFactory.CategoryOrderId}/composition");
        Assert.Equal((3, 70m, 100m, 500m, 2670m),
            (Assert.Single(current!.Lines).Quantity, current.ShippingCost, current.Discount, current.Prepayment, current.Total));
        var details = await manager.GetFromJsonAsync<OrderDetails>($"/orders/{ApiFactory.CategoryOrderId}");
        Assert.Equal((2670m, ApiFactory.Accepted), (details!.Total, details.StatusId));

        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PutAsJsonAsync($"/orders/{ApiFactory.CategoryOrderId}/composition",
            new UpdateOrderCompositionRequest([new(ApiFactory.CategoryProductId, SizeM, 0, 900)]))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync("/orders/9999/composition")).StatusCode);
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync($"/orders/{ApiFactory.CategoryOrderId}/composition")).StatusCode);

        async Task<decimal> SewnAsync() =>
            (await admin.GetFromJsonAsync<ProductionModelDto>($"/production/models/{modelId}"))!.Sizes[0].SewnStock;
    }
}
