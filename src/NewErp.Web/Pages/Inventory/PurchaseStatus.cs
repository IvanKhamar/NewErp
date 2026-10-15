using MudBlazor;

using NewErp.Domain.Stock;

namespace NewErp.Web.Pages.Inventory;

/// <summary>Назви й кольори статусів замовлення постачальнику.</summary>
public static class PurchaseStatus
{
    public static readonly IReadOnlyList<string> All = [PurchaseOrder.Draft, PurchaseOrder.Ordered, PurchaseOrder.Received, PurchaseOrder.Cancelled];

    public static string Label(string status) => status switch
    {
        PurchaseOrder.Draft => "Чернетка",
        PurchaseOrder.Ordered => "Замовлено",
        PurchaseOrder.Received => "Отримано",
        PurchaseOrder.Cancelled => "Скасовано",
        _ => status,
    };

    public static Color Color(string status) => status switch
    {
        PurchaseOrder.Draft => MudBlazor.Color.Default,
        PurchaseOrder.Ordered => MudBlazor.Color.Info,
        PurchaseOrder.Received => MudBlazor.Color.Success,
        _ => MudBlazor.Color.Dark,
    };
}
