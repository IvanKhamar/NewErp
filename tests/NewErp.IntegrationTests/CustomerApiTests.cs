using System.Net;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Common;
using NewErp.Application.Customers;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.IntegrationTests;

/// <summary>Покупці. Окрема тестова база, бо тест додає покупця, групу й адресу.</summary>
public class CustomerApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const int CustomerId = 1, GroupId = 1;

    [MySqlFact]
    public async Task Card_FindsOrdersByPhone_WithRisk_AndNotes()
    {
        await SeedAsync();
        var manager = await factory.LoginAsync(ApiFactory.Manager);

        var list = await manager.GetFromJsonAsync<PagedResult<CustomerListItem>>("/customers?search=Коваль");
        var item = Assert.Single(list!.Items);
        Assert.Equal((CustomerId, "Олена Коваль", "Роздріб", 0), (item.Id, item.Name, item.GroupName, item.OrderCount));

        // Замовлення гостя з тим самим телефоном, записаним інакше, теж у картці.
        (await manager.PostAsJsonAsync($"/customers/{CustomerId}/notes", new AddCustomerNoteRequest(" Просила дзвонити ввечері "))).EnsureSuccessStatusCode();
        var card = await manager.GetFromJsonAsync<CustomerDetails>($"/customers/{CustomerId}");
        var order = Assert.Single(card!.Orders);
        Assert.Equal((ApiFactory.CancelledOrderId, true, "Відмінено"), (order.Id, order.ByPhone, order.StatusName));
        Assert.Equal((1, 1, 0m, "Red"), (card.Stats.OrderCount, card.Stats.CancelledCount, card.Stats.Spent, card.Stats.RiskLevel));
        Assert.Equal(("Київ", true), (Assert.Single(card.Addresses).City, card.Addresses[0].IsDefault));
        Assert.Equal("Просила дзвонити ввечері", Assert.Single(card.Notes).Comment);

        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsJsonAsync($"/customers/{CustomerId}/notes", new AddCustomerNoteRequest(" "))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync("/customers/9999")).StatusCode);
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync("/customers")).StatusCode);
    }

    private async Task SeedAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        db.DdCustomerGroupDescriptions.Add(new DdCustomerGroupDescription { CustomerGroupId = GroupId, LanguageId = ApiFactory.LanguageId, Name = "Роздріб", Description = "" });
        db.DdCustomers.Add(new DdCustomer
        {
            CustomerId = CustomerId, CustomerGroupId = GroupId, LanguageId = ApiFactory.LanguageId, Firstname = "Олена", Lastname = "Коваль",
            Email = "olena@example.com", Telephone = "0935554433", Fax = "", Password = "", Salt = "", CustomField = "", Ip = "",
            Status = true, Token = "", Code = "", AddressId = 7, DateAdded = new DateTime(2024, 5, 1),
        });
        db.DdAddresses.Add(new DdAddress
        {
            AddressId = 7, CustomerId = CustomerId, Firstname = "Олена", Lastname = "Коваль", Company = "", Address1 = "Відділення №1",
            Address2 = "", City = "Київ", Postcode = "", CustomField = "",
        });
        await db.SaveChangesAsync();
    }
}
