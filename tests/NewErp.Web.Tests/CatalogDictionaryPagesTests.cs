using System.Net;
using System.Text.Json.Nodes;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Auth;
using NewErp.Application.Catalog;
using NewErp.Application.Common;
using NewErp.Web.Api;
using NewErp.Web.Pages.Sales;

namespace NewErp.Web.Tests;

/// <summary>Сторінки міток і пресетів (пункт 47) та категорій (пункт 48) з підміненим API.</summary>
public class CatalogDictionaryPagesTests : WebTestContext
{
    private static readonly CategoryEditDto Dresses = new()
    {
        Id = 6, Name = "Сукні", ParentId = 5, Description = "<p>Лляні</p>", ProductionPeriod = 14, FilterIds = [301],
    };

    public CatalogDictionaryPagesTests()
    {
        Services.AddScoped<CatalogApi>();
        Api.Respond("/catalog/labels", HttpStatusCode.OK, new List<LabelDto> { new(1, "Хіт", "#ff0000", "#ffffff", 0, true, 3) });
        Api.Respond("/catalog/labels", HttpStatusCode.Created, new LabelDto(2, "Новинка"), HttpMethod.Post);
        Api.Respond("/catalog/search-presets", HttpStatusCode.OK, new List<SearchPresetDto> { new(1, "Літо", "/summer", 0, true) });
        Api.Respond("/catalog/categories", HttpStatusCode.OK, new List<CategoryListItem>
        {
            new(5, "Одяг", 0, 0, true, 2), new(6, "Одяг > Сукні", 5, 1, true, 4),
        });
        Api.Respond("/catalog/categories", HttpStatusCode.Created, Dresses with { Id = 7, Name = "Літні" }, HttpMethod.Post);
        Api.Respond("/catalog/categories/filters", HttpStatusCode.OK, new List<LookupItem> { new(301, "3: S") });
        Api.Respond("/catalog/categories/6", HttpStatusCode.OK, Dresses);
        Api.Respond("/catalog/categories/6", HttpStatusCode.OK, Dresses, HttpMethod.Put);
    }

    [Fact]
    public void Labels_ShowCountsAndColours_AndANewLabelIsSaved()
    {
        var page = Render<Labels>();
        page.WaitForAssertion(() => Assert.Contains("Хіт", page.Find("[data-test=labels]").TextContent));
        Assert.Contains("Літо", page.Find("[data-test=presets]").TextContent);

        page.Find("input[data-test=label-name]").Change("Новинка");
        page.Find("button[data-test=label-save]").Click();
        page.WaitForAssertion(() => Assert.Contains("/catalog/labels", Api.Bodies.Keys));
        Assert.Equal("Новинка", JsonNode.Parse(Api.Bodies["/catalog/labels"])!["name"]!.GetValue<string>());
    }

    [Fact]
    public void Categories_ListFullNames_AndOnlyTheAdminRepairsTheTree()
    {
        var auth = AddAuthorization();
        auth.SetAuthorized("Тест");
        auth.SetRoles(Roles.Manager);
        var page = Render<Categories>();
        page.WaitForAssertion(() => Assert.Contains("Одяг > Сукні", page.Find("[data-test=categories]").TextContent));
        Assert.Empty(page.FindAll("[data-test=repair]"));
    }

    [Fact]
    public void CategoryForm_LoadsAndSavesTheCategory()
    {
        var auth = AddAuthorization();
        auth.SetAuthorized("Тест");
        auth.SetRoles(Roles.Admin);
        var page = Render<CategoryEdit>(p => p.Add(x => x.Id, 6));
        page.WaitForAssertion(() => Assert.Equal("Сукні", page.Find("input[data-test=name]").GetAttribute("value")));
        Assert.NotEmpty(page.FindAll("[data-test=delete]"));

        page.Find("input[data-test=name]").Change("Сукні лляні");
        page.Find("button[data-test=save]").Click();
        page.WaitForAssertion(() => Assert.Contains("/catalog/categories/6", Api.Bodies.Keys));
        var body = Api.Bodies["/catalog/categories/6"];
        Assert.Equal("Сукні лляні", JsonNode.Parse(body)!["name"]!.GetValue<string>());
        Assert.Contains("\"filterIds\":[301]", body);
        Assert.Contains("\"productionPeriod\":14", body);
    }

    [Fact]
    public void NewCategory_GoesToItsPageAfterSaving()
    {
        var auth = AddAuthorization();
        auth.SetAuthorized("Тест");
        auth.SetRoles(Roles.Manager);
        var page = Render<CategoryEdit>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("input[data-test=name]")));
        page.Find("input[data-test=name]").Change("Літні");
        page.Find("button[data-test=save]").Click();
        var navigation = Services.GetRequiredService<NavigationManager>();
        page.WaitForAssertion(() => Assert.EndsWith(CategoryRoutes.Edit(7), navigation.Uri));
    }
}
