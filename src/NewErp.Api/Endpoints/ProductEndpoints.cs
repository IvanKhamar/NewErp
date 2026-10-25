using NewErp.Api.Auth;
using NewErp.Application.Catalog;

namespace NewErp.Api.Endpoints;

/// <summary>Товари магазину; той самий контракт, що й у Java-версії (ProductController).</summary>
public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var products = app.MapGroup("/catalog/products").WithTags("Товари").RequireAuthorization(Policies.Orders);

        products.MapGet("/", ([AsParameters] ProductFilter filter, ProductService service, CancellationToken ct) => service.ListAsync(filter, ct))
            .WithSummary("Товари магазину з фільтрами (пошук, категорія, мітка, увімкнені) і пагінацією");

        products.MapGet("/{id:int}", (int id, ProductService service, CancellationToken ct) => service.GetAsync(id, ct))
            .WithSummary("Картка товару: розміри із залишками (лише для читання), мітки й категорії");

        products.MapGet("/labels", (ProductService service, CancellationToken ct) => service.LabelsAsync(ct))
            .WithSummary("Усі мітки товарів");

        products.MapGet("/categories", (ProductService service, CancellationToken ct) => service.CategoriesAsync(ct))
            .WithSummary("Увімкнені категорії");
    }
}
