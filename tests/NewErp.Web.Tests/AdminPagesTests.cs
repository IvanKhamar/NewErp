using System.Net;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Auth;
using NewErp.Application.Staff;
using NewErp.Web.Api;
using NewErp.Web.Pages.Admin;

namespace NewErp.Web.Tests;

public class AdminPagesTests : WebTestContext
{
    public AdminPagesTests()
    {
        Services.AddScoped<AdminApi>();
        var auth = AddAuthorization();
        auth.SetAuthorized("Адмін");
        auth.SetRoles(Roles.Admin);
    }

    [Fact]
    public void Users_ByDefaultShowOnlyThoseWithAccess_SwitchShowsEveryone()
    {
        Api.Respond("/users", HttpStatusCode.OK, new List<UserDto>
        {
            new(1, "admin", "Кузьма Гладух", "", true, true, Roles.Admin, DateTime.Now),
            new(8, "ui-test", "Тест Інтерфейсу", "", true, false, null, null),
        });

        var page = Render<Users>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("tbody tr")));
        Assert.Contains("Адміністратор", page.Find("tbody tr").TextContent);

        page.Find("input[type=checkbox]").Change(false);

        Assert.Equal(2, page.FindAll("tbody tr").Count);
        Assert.Contains("немає доступу", page.FindAll("tbody tr")[1].TextContent);
    }

    [Fact]
    public async Task RoleDialog_ForSelf_OnlyAdminCanBeChosen()
    {
        // Вміст MudDialog рендериться лише всередині MudDialogProvider, тож відкриваємо діалог так само, як сторінка.
        var provider = Render<MudBlazor.MudDialogProvider>();
        var dialogs = Services.GetRequiredService<MudBlazor.IDialogService>();
        await provider.InvokeAsync(() => dialogs.ShowAsync<RoleDialog>("Роль", new MudBlazor.DialogParameters<RoleDialog>
        {
            { d => d.Current, Roles.Admin }, { d => d.IsSelf, true },
        }));

        var radios = provider.FindAll("input[type=radio]");
        Assert.Equal(3, radios.Count);
        Assert.Equal(1, radios.Count(r => !r.HasAttribute("disabled"))); // лише «Адміністратор»
    }

    [Fact]
    public void Bonuses_ShowsRowsAndTotal()
    {
        Api.Respond("/staff/bonuses", HttpStatusCode.OK, new BonusReport(
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 16), 1,
            [new ManagerBonusRow(4, "Софія Григоришина", 3, 30100, 301), new ManagerBonusRow(5, "Майя Унич", 2, 23400, 234)]));

        var page = Render<Bonuses>();

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("tbody tr").Count));
        Assert.Contains("535,00", page.Markup); // 301 + 234
    }
}
