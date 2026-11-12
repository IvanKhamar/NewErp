using System.Net;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Auth;
using NewErp.Application.Catalog;
using NewErp.Application.Common;
using NewErp.Web.Api;
using NewErp.Web.Pages.Sales;

namespace NewErp.Web.Tests;

/// <summary>Уся форма товару, швидка зміна ціни й «зняти NEW» (пункти 43–45, 76) з підробленим API; товари вигадані.</summary>
public class ProductFormPagesTests : WebTestContext
{
    private static readonly DateTime Version = new(2026, 11, 1, 10, 0, 0);

    private static ProductFormLookups Lookups() => new(
        [new(30, "Сукні")], [new(1, "NEW")], [new(70, "7: NEW")], [new(47, "M"), new(48, "L")],
        [new ProductOptionLookup(20, "Колір", "select", true, [new(201, "Червоний"), new(202, "Синій")]),
         new ProductOptionLookup(21, "Гравіювання", "text", false, [])],
        [new(5, "Склад: Тканина")], []);

    private static ProductEditDto Product() => new()
    {
        Id = 70, DateModified = Version, Name = "Сукня лляна", Model = "SL-01", Price = 1500m, CategoryIds = [30],
        Sizes = [new ProductSizeEditDto(47, "M", 0, 3)],
        Options = [new ProductOptionDto { OptionId = 20, Name = "Колір", Values = [new ProductOptionValueEditDto(201, "Червоний", 100, "+", 0)] }],
        Attributes = [new ProductAttributeDto(5, "Льон")],
    };

    [Fact]
    public void Edit_ShowsTheWholeProduct_AndSavesWithItsVersion()
    {
        Services.AddScoped<ProductsApi>();
        Api.Respond("/catalog/products/form-lookups", HttpStatusCode.OK, Lookups());
        Api.Respond("/catalog/products/70/edit", HttpStatusCode.OK, Product());
        Api.Respond("/catalog/products/70", HttpStatusCode.OK, Product() with { DateModified = Version.AddMinutes(1) }, HttpMethod.Put);

        var page = Render<ProductEditor>(p => p.Add(c => c.Id, 70));
        page.WaitForAssertion(() => Assert.Contains("Червоний", page.Find("[data-test=options]").TextContent));
        Assert.Contains("залишок 3", page.Find("[data-test=sizes]").TextContent);
        Assert.Contains("Гравіювання", page.Find("[data-test=options]").TextContent);
        Assert.Contains("Копіювати товар", page.Markup);

        page.Find("button[data-test=save]").Click();
        page.WaitForAssertion(() => Assert.True(Api.Bodies.ContainsKey("/catalog/products/70")));
        var body = Api.Bodies["/catalog/products/70"];
        Assert.Contains("\"dateModified\":\"2026-11-01T10:00:00\"", body);
        Assert.Contains("\"sizes\":[{\"optionValueId\":47", body);
        Assert.Contains("\"optionId\":20", body);
        Assert.DoesNotContain("\"optionId\":21", body); // текстову опцію не обрано
        Assert.Contains("\"attributeId\":5", body);
    }

    [Fact]
    public void Edit_RefusesRelatedIdsThatAreNotNumbers_BeforeSending()
    {
        Services.AddScoped<ProductsApi>();
        Api.Respond("/catalog/products/form-lookups", HttpStatusCode.OK, Lookups());
        Api.Respond("/catalog/products/70/edit", HttpStatusCode.OK, Product());

        var page = Render<ProductEditor>(p => p.Add(c => c.Id, 70));
        page.WaitForAssertion(() => Assert.NotNull(page.Find("input[data-test=related]")));
        page.Find("input[data-test=related]").Change("1, x");
        page.Find("button[data-test=save]").Click();

        Assert.DoesNotContain("/catalog/products/70", Api.Bodies.Keys);
    }

    [Fact]
    public void New_CreatesTheProduct_AndOpensItsForm()
    {
        Services.AddScoped<ProductsApi>();
        Api.Respond("/catalog/products/form-lookups", HttpStatusCode.OK, Lookups());
        Api.Respond("/catalog/products", HttpStatusCode.Created, new CreatedProductDto(77), HttpMethod.Post);

        var page = Render<ProductEditor>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("button[data-test=save]")));
        Assert.DoesNotContain("Копіювати товар", page.Markup);
        page.Find("input[data-test=name]").Change("Сукня нова");
        page.Find("input[data-test=model]").Change("SN-1");
        page.Find("button[data-test=save]").Click();

        page.WaitForAssertion(() => Assert.EndsWith("catalog/products/77/edit", Services.GetRequiredService<NavigationManager>().Uri));
        Assert.Contains("\"model\":\"SN-1\"", Api.Bodies["/catalog/products"]); // кирилицю JSON екранує
    }

    [Fact]
    public void List_SavesAQuickPrice_WithTheRowVersion_AndRemovesNew()
    {
        Services.AddScoped<ProductsApi>();
        var auth = AddAuthorization();
        auth.SetAuthorized("Олена");
        auth.SetRoles(Roles.Admin);
        Api.Respond("/catalog/products/categories", HttpStatusCode.OK, new List<LookupItem>());
        Api.Respond("/catalog/products/labels", HttpStatusCode.OK, new List<LookupItem>());
        Api.Respond("/catalog/products", HttpStatusCode.OK, new PagedResult<ProductListItem>(
            [new(70, "Сукня лляна", "SL-01", 1500m, true, 6, null, Version)], 1, 50, 1));
        Api.Respond("/catalog/products/70", HttpStatusCode.OK, new ProductCardDto(70, "Сукня лляна", "SL-01", 1450m, true, 6, null,
            Version.AddMinutes(1), [], [], []), HttpMethod.Patch);
        Api.Respond("/catalog/products/remove-new", HttpStatusCode.OK, new RemoveNewResult([70], true, true), HttpMethod.Post);

        var page = Render<Products>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("input[data-test=price-70]")));
        Assert.Contains("Новий товар", page.Markup);
        Assert.Contains("Імпорт CSV", page.Markup);
        page.Find("input[data-test=price-70]").Change("1450");
        page.WaitForAssertion(() => Assert.NotNull(page.Find("button[data-test=save-price-70]")));
        page.Find("button[data-test=save-price-70]").Click();
        page.WaitForAssertion(() => Assert.True(Api.Bodies.ContainsKey("/catalog/products/70")));
        Assert.Contains("\"price\":1450", Api.Bodies["/catalog/products/70"]);
        Assert.Contains("\"dateModified\":\"2026-11-01T10:00:00\"", Api.Bodies["/catalog/products/70"]);

        page.Find("[data-test=remove-new] input").Change("PRINT-1");
        page.Find("[data-test=remove-new] button").Click();
        page.WaitForAssertion(() => Assert.Contains("PRINT-1", Api.Bodies["/catalog/products/remove-new"]));
    }
}
