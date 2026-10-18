using System.Net;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using MudBlazor;

using NewErp.Application.Common;
using NewErp.Application.Orders;
using NewErp.Domain.Orders;
using NewErp.Web.Api;
using NewErp.Web.Pages.Sales;

namespace NewErp.Web.Tests;

public class NewOrderPageTests : WebTestContext
{
    private static readonly ProductPickDto Dress = new(5, "Сукня лляна", "SL-01", 1500, 3,
        [new(50, 501, "Розмір", "S", 1, 0), new(50, 502, "Розмір", "M", 2, 1600)]);

    public NewOrderPageTests()
    {
        Services.AddScoped<OrdersApi>();
        var auth = AddAuthorization();
        auth.SetAuthorized("Менеджер");
        auth.SetRoles("Manager");
        Api.Respond("/orders/statuses", HttpStatusCode.OK, new List<OrderStatusDto> { new(24, "1) Уточнюємо", "#48f9f9") });
        Api.Respond("/orders/sources", HttpStatusCode.OK, new List<LookupItem> { new(1, "Instagram") });
        Api.Respond("/orders/managers", HttpStatusCode.OK, new List<LookupItem> { new(1, "Марта") });
        Api.Respond("/orders/new/methods", HttpStatusCode.OK, new OrderMethodsDto(
            [new("novaposhta_warehouse.novaposhta_warehouse", "Нова Пошта")], [new("cod", "Накладений платіж")]));
    }

    [Fact]
    public async Task Save_SendsLinesWithSizeAndOptionPrice_ThenOpensOrder()
    {
        Api.Respond("/orders", HttpStatusCode.Created, new CreateOrderResult(700, ["Пошитих бракує"], Risk(CustomerRiskLevel.New)), HttpMethod.Post);
        var page = Render<NewOrder>();
        page.WaitForAssertion(() => page.Find("[data-test=save]"));

        // Автодоповнення MudBlazor працює через JavaScript, тож у тесті обираємо товар напряму.
        var product = page.FindComponent<MudAutocomplete<ProductPickDto>>();
        await page.InvokeAsync(() => product.Instance.ValueChanged.InvokeAsync(Dress));
        page.Find("[data-test=add-product]").Click();
        var size = page.FindComponent<MudSelect<int?>>();
        await page.InvokeAsync(() => size.Instance.ValueChanged.InvokeAsync(502));
        await page.InvokeAsync(() => page.FindComponents<MudTextField<string>>()[1].Instance.ValueChanged.InvokeAsync("Ганна")); // ім'я

        page.WaitForAssertion(() => Assert.Contains("1600 грн", page.Find("[data-test=totals]").TextContent)); // ціна розміру M замінила базову
        page.Find("[data-test=save]").Click();

        page.WaitForAssertion(() => Assert.EndsWith("orders/700", Services.GetRequiredService<NavigationManager>().Uri));
        var body = Api.Bodies["/orders"];
        Assert.Contains("\"productId\":5,\"productOptionValueId\":502,\"quantity\":1,\"price\":1600", body);
        Assert.Contains("\"shippingCode\":\"novaposhta_warehouse.novaposhta_warehouse\"", body);
        Assert.Contains("\"paymentCode\":\"cod\"", body);
    }

    [Fact]
    public void Save_ServerError_IsShown()
    {
        Api.Respond("/orders", HttpStatusCode.BadRequest, new { title = "Кошик не може бути порожнім." }, HttpMethod.Post);
        var page = Render<NewOrder>();
        page.WaitForAssertion(() => page.Find("[data-test=save]"));

        page.Find("[data-test=save]").Click();

        page.WaitForAssertion(() => Assert.Contains("Кошик", page.Find("[data-test=error]").TextContent));
    }

    [Fact]
    public void PickCustomer_FillsContacts_AndWarnsAboutCancelledOrders()
    {
        Api.Respond("/orders/customer-risk", HttpStatusCode.OK, Risk(CustomerRiskLevel.Red) with { TotalOrders = 2, CancelledOrders = 1, CancelledOrderIds = [11] });
        var page = Render<NewOrder>();
        page.WaitForAssertion(() => page.Find("[data-test=save]"));

        var customer = page.FindComponent<MudAutocomplete<CustomerPickDto>>();
        page.InvokeAsync(() => customer.Instance.ValueChanged.InvokeAsync(
            new CustomerPickDto(0, "Ганна", "Нова", "+380501112233", "", "@hanna", "Київ", "", "Відділення №1", 11)));

        page.WaitForAssertion(() => Assert.Contains("№ 11", page.Find("[data-test=risk]").TextContent));
        Assert.Contains("value=\"+380501112233\"", page.Markup);
    }

    private static CustomerRiskDto Risk(CustomerRiskLevel level) => new(level, 0, 0, [], 0, "Потенційний фрод-ризик.");
}
