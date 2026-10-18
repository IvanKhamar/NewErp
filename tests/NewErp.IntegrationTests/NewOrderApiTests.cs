using System.Net;
using System.Net.Http.Json;

using NewErp.Application.Orders;
using NewErp.Domain.Orders;

namespace NewErp.IntegrationTests;

public class NewOrderApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CreateOrderRequest Request(string phone, int quantity = 2) => new(
        "Ганна", "Нова", phone, "", "@hanna", 1, ApiFactory.Pending, ApiFactory.ManagerUserId,
        "novaposhta_warehouse.novaposhta_warehouse", "Нова Пошта", "Київ", "", "Відділення №1", "", "",
        "cod", "Накладений платіж",
        [new NewOrderLineRequest(ApiFactory.CategoryProductId, 502, quantity, 900m)],
        ShippingCost: 0, Discount: 100, Prepayment: 200, Comment: "Дзвінок");

    [MySqlFact]
    public async Task Create_WritesOrderLinesOptionsAndStatus()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);

        var response = await client.PostAsJsonAsync("/orders", Request("+380501112233"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<CreateOrderResult>())!;
        Assert.Equal(CustomerRiskLevel.New, created.Risk.Level);

        var order = (await client.GetFromJsonAsync<OrderDetails>($"/orders/{created.OrderId}"))!;
        Assert.Equal((1700m, ApiFactory.Pending, "Ганна Нова", "Instagram"), (order.Total, order.StatusId, order.Customer, order.SourceName));
        Assert.Equal(ApiFactory.ManagerUserId, order.ManagerId);
        var line = Assert.Single(order.Products);
        Assert.Equal(("Сорочка лляна оверсайз", "T-2", 2, 1800m), (line.Name, line.Model, line.Quantity, line.Total));
        var history = Assert.Single(order.StatusHistory);
        Assert.Equal((0, ApiFactory.Pending), (history.OldStatusId, history.NewStatusId));

        // Нове замовлення одразу видно в списку, а клієнта — в пошуку.
        var customers = await client.GetFromJsonAsync<List<CustomerPickDto>>("/orders/new/customers?search=1112233");
        Assert.Equal(("Ганна", "@hanna", created.OrderId), (customers![0].Firstname, customers[0].Instagram, customers[0].LastOrderId));
    }

    [MySqlFact]
    public async Task Create_InvalidOrder_Returns400()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);

        var response = await client.PostAsJsonAsync("/orders", Request("+380501112244", quantity: 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [MySqlFact]
    public async Task ProductSearch_ReturnsPriceAndSizes()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);

        var products = await client.GetFromJsonAsync<List<ProductPickDto>>("/orders/new/products?search=лляна");

        var product = Assert.Single(products!);
        Assert.Equal((ApiFactory.CategoryProductId, 900m), (product.Id, product.Price));
        var size = Assert.Single(product.Options);
        Assert.Equal((502, "Розмір", "M"), (size.ProductOptionValueId, size.OptionName, size.Value));
    }

    [MySqlFact]
    public async Task CustomerRisk_FindsCancelledOrderByPhoneInAnyFormat()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);

        var risk = await client.GetFromJsonAsync<CustomerRiskDto>("/orders/customer-risk?telephone=0935554433");

        Assert.Equal((CustomerRiskLevel.Red, 1), (risk!.Level, risk.TotalOrders));
        Assert.Equal([ApiFactory.CancelledOrderId], risk.CancelledOrderIds);
    }

    [MySqlFact]
    public async Task Methods_ComeFromExistingOrders()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);

        var methods = await client.GetFromJsonAsync<OrderMethodsDto>("/orders/new/methods");

        Assert.Contains(new OrderMethodDto("cod", "Накладений платіж"), methods!.Payment);
        Assert.Contains(new OrderMethodDto("novaposhta_warehouse.novaposhta_warehouse", "Нова Пошта"), methods.Shipping);
    }

    [MySqlFact]
    public async Task Storekeeper_CannotCreateOrders()
    {
        var client = await factory.LoginAsync(ApiFactory.Storekeeper);

        var response = await client.PostAsJsonAsync("/orders", Request("+380501112255"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
