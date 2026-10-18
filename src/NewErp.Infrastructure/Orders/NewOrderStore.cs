using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Orders;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.Infrastructure.Orders;

public sealed class NewOrderStore(ErpDbContext db, IOptions<ErpOptions> options) : INewOrderStore
{
    private readonly int _languageId = options.Value.LanguageId;

    // id України в довіднику країн стандартного OpenCart; беремо, лише якщо в базі ще немає жодного замовлення.
    private const int UkraineCountryId = 220;

    public async Task<IReadOnlyList<ProductPickDto>> SearchProductsAsync(string term, int limit, CancellationToken ct)
    {
        var id = int.TryParse(term, out var parsed) ? parsed : 0;
        var ids = await (
            from p in db.DdProducts
            join d in db.DdProductDescriptions on new { p.ProductId, LanguageId = _languageId } equals new { d.ProductId, d.LanguageId }
            where p.Status && (p.ProductId == id || d.Name.Contains(term) || p.Model.Contains(term))
            orderby d.Name
            select p.ProductId)
            .Take(limit)
            .ToListAsync(ct);
        return await GetProductsAsync(ids, ct);
    }

    public async Task<IReadOnlyList<ProductPickDto>> GetProductsAsync(IReadOnlyCollection<int> productIds, CancellationToken ct)
    {
        // Лише потрібні колонки: date_available у справжній базі може бути '0000-00-00', який DateTime не прочитає.
        var products = await (
            from p in db.DdProducts
            join d in db.DdProductDescriptions on new { p.ProductId, LanguageId = _languageId } equals new { d.ProductId, d.LanguageId }
            where productIds.Contains(p.ProductId)
            orderby d.Name
            select new { p.ProductId, d.Name, p.Model, p.Price, p.Quantity })
            .ToListAsync(ct);

        var options = await (
            from v in db.DdProductOptionValues
            join od in db.DdOptionDescriptions on new { v.OptionId, LanguageId = _languageId } equals new { od.OptionId, od.LanguageId } into ods
            from od in ods.DefaultIfEmpty()
            join vd in db.DdOptionValueDescriptions on new { v.OptionValueId, LanguageId = _languageId } equals new { vd.OptionValueId, vd.LanguageId } into vds
            from vd in vds.DefaultIfEmpty()
            where productIds.Contains(v.ProductId)
            orderby v.ProductOptionValueId
            select new { v.ProductId, v.ProductOptionId, v.ProductOptionValueId, OptionName = od == null ? "" : od.Name, Value = vd == null ? "" : vd.Name, v.Quantity, v.Price })
            .ToListAsync(ct);
        var byProduct = options.ToLookup(o => o.ProductId);

        return products.Select(p => new ProductPickDto(
            p.ProductId, p.Name, p.Model, p.Price, p.Quantity,
            byProduct[p.ProductId]
                .Select(o => new ProductOptionValueDto(o.ProductOptionId, o.ProductOptionValueId, o.OptionName, o.Value, o.Quantity, o.Price))
                .ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<CustomerPickDto>> SearchCustomersAsync(string term, int limit, CancellationToken ct)
    {
        var rows = await db.DdOrders.AsNoTracking()
            .Where(o => o.OrderStatusId > 0 && (o.Telephone.Contains(term) || o.Email.Contains(term)
                || o.Firstname.Contains(term) || o.Lastname.Contains(term) || (o.Firstname + " " + o.Lastname).Contains(term)))
            .OrderByDescending(o => o.OrderId)
            .Take(limit * 5)
            .Select(o => new CustomerPickDto(
                o.CustomerId, o.Firstname, o.Lastname, o.Telephone, o.Email, o.Instagram,
                o.ShippingCity, o.ShippingAddress1, o.ShippingAddress2, o.OrderId))
            .ToListAsync(ct);

        // Один клієнт — один рядок (дані з його останнього замовлення).
        return rows
            .DistinctBy(c => Domain.Orders.CustomerRisk.PhoneKey(c.Telephone) ?? $"{c.Firstname} {c.Lastname} {c.Email}".ToLowerInvariant())
            .Take(limit)
            .ToList();
    }

    public async Task<IReadOnlyList<(int OrderId, int StatusId)>> FindCustomerOrdersAsync(
        string? phoneKey, string? firstname, string? lastname, string? email, DateTime since, CancellationToken ct)
    {
        // Телефон у базі записаний як завгодно («+38 (067) 123-45-67»), тому порівнюємо лише цифри, як старий сайт.
        var rows = await db.DdOrders.AsNoTracking()
            .Where(o => o.DateAdded >= since && o.OrderStatusId > 0)
            .Where(o => (phoneKey != null
                    && o.Telephone.Replace("+", "").Replace("-", "").Replace(" ", "").Replace("(", "").Replace(")", "").EndsWith(phoneKey))
                || (firstname != null && lastname != null && o.Firstname == firstname && o.Lastname == lastname)
                || (email != null && o.Email == email))
            .OrderByDescending(o => o.DateAdded)
            .Select(o => new { o.OrderId, o.OrderStatusId, o.Telephone, o.Firstname, o.Lastname, o.Email })
            .ToListAsync(ct);

        return rows
            .Where(o => (phoneKey != null && Domain.Orders.CustomerRisk.PhoneKey(o.Telephone) == phoneKey)
                || (firstname != null && lastname != null && Same(o.Firstname, firstname) && Same(o.Lastname, lastname))
                || (email != null && Same(o.Email, email)))
            .Select(o => (o.OrderId, o.OrderStatusId))
            .ToList();
    }

    public async Task<OrderMethodsDto> ListMethodsAsync(CancellationToken ct)
    {
        var shipping = await db.DdOrders.AsNoTracking()
            .Where(o => o.ShippingMethod != "")
            .GroupBy(o => new { o.ShippingCode, o.ShippingMethod })
            .Select(g => new { g.Key.ShippingCode, g.Key.ShippingMethod, Count = g.Count() })
            .ToListAsync(ct);
        var payment = await db.DdOrders.AsNoTracking()
            .Where(o => o.PaymentMethod != "")
            .GroupBy(o => new { o.PaymentCode, o.PaymentMethod })
            .Select(g => new { g.Key.PaymentCode, g.Key.PaymentMethod, Count = g.Count() })
            .ToListAsync(ct);

        return new OrderMethodsDto(
            shipping.OrderByDescending(m => m.Count).Select(m => new OrderMethodDto(m.ShippingCode, m.ShippingMethod)).ToList(),
            payment.OrderByDescending(m => m.Count).Select(m => new OrderMethodDto(m.PaymentCode, m.PaymentMethod)).ToList());
    }

    public Task<bool> SourceExistsAsync(int sourceId, CancellationToken ct) =>
        db.DdOrderSources.AnyAsync(s => s.OrderSourceId == sourceId, ct);

    public async Task<int> CreateAsync(NewOrderRecord order, CancellationToken ct)
    {
        var r = order.Request;
        // Поля магазину (назва, валюта, країна, формат адреси) задає налаштування OpenCart, якого в нас немає,
        // тому беремо їх з останнього замовлення сайту.
        var template = await db.DdOrders.AsNoTracking()
            .OrderByDescending(o => o.OrderId)
            .Select(o => new
            {
                o.InvoicePrefix, o.StoreId, o.StoreName, o.StoreUrl, o.LanguageId, o.CurrencyId, o.CurrencyCode, o.CurrencyValue,
                o.ShippingCountry, o.ShippingCountryId, o.ShippingZone, o.ShippingZoneId, o.ShippingAddressFormat,
            })
            .FirstOrDefaultAsync(ct);

        var row = new DdOrder
        {
            InvoicePrefix = template?.InvoicePrefix ?? "",
            StoreId = template?.StoreId ?? 0,
            StoreName = template?.StoreName ?? "",
            StoreUrl = template?.StoreUrl ?? "",
            CustomerId = 0,
            CustomerGroupId = 0,
            Firstname = r.Firstname.Trim(),
            Lastname = (r.Lastname ?? "").Trim(),
            Email = (r.Email ?? "").Trim(),
            Telephone = (r.Telephone ?? "").Trim(),
            Instagram = (r.Instagram ?? "").Trim(),
            Fax = "",
            CustomField = "[]",
            PaymentCustomField = "[]",
            ShippingCustomField = "[]",
            PaymentCompany = "",
            ShippingCompany = "",
            PaymentPostcode = "",
            ShippingPostcode = "",
            PaymentMethod = r.PaymentMethod.Trim(),
            PaymentCode = (r.PaymentCode ?? "").Trim(),
            ShippingMethod = r.ShippingMethod.Trim(),
            ShippingCode = (r.ShippingCode ?? "").Trim(),
            ShippingHouse = (r.House ?? "").Trim(),
            ShippingFlat = (r.Flat ?? "").Trim(),
            Comment = (r.Comment ?? "").Trim(),
            Total = order.Draft.Total,
            Prepayment = order.Draft.Prepayment,
            OrderStatusId = 0,
            OrderSourceId = r.OrderSourceId,
            Tracking = "",
            LanguageId = template?.LanguageId ?? _languageId,
            CurrencyId = template?.CurrencyId ?? 0,
            CurrencyCode = template?.CurrencyCode ?? "UAH",
            CurrencyValue = template?.CurrencyValue ?? 1,
            Ip = "",
            ForwardedIp = "",
            UserAgent = "NewErp",
            AcceptLanguage = "",
            DateAdded = order.CreatedAt,
            DateModified = order.CreatedAt,
        };
        // Як на старому сайті: адреса оплати й доставки однакові.
        row.PaymentFirstname = row.ShippingFirstname = row.Firstname;
        row.PaymentLastname = row.ShippingLastname = row.Lastname;
        row.PaymentCity = row.ShippingCity = (r.City ?? "").Trim();
        row.PaymentAddress1 = row.ShippingAddress1 = (r.Address1 ?? "").Trim();
        row.PaymentAddress2 = row.ShippingAddress2 = (r.Address2 ?? "").Trim();
        row.PaymentCountry = row.ShippingCountry = template?.ShippingCountry ?? "Україна";
        row.PaymentCountryId = row.ShippingCountryId = template?.ShippingCountryId ?? UkraineCountryId;
        row.PaymentZone = row.ShippingZone = template?.ShippingZone ?? "";
        row.PaymentZoneId = row.ShippingZoneId = template?.ShippingZoneId ?? 0;
        row.PaymentAddressFormat = row.ShippingAddressFormat = template?.ShippingAddressFormat ?? "";

        // Таблиці OpenCart — MyISAM, без транзакцій: зберігаємо по кроках (спершу замовлення, щоб отримати id).
        db.DdOrders.Add(row);
        await db.SaveChangesAsync(ct);

        foreach (var line in order.Lines)
        {
            var product = new DdOrderProduct
            {
                OrderId = row.OrderId,
                ProductId = line.Line.ProductId,
                Name = line.Name,
                Model = line.Model,
                Quantity = line.Line.Quantity,
                Price = line.Line.Price,
                Total = line.Line.Total,
            };
            db.DdOrderProducts.Add(product);
            await db.SaveChangesAsync(ct);

            if (line.Option is { } option)
                db.DdOrderOptions.Add(new DdOrderOption
                {
                    OrderId = row.OrderId,
                    OrderProductId = product.OrderProductId,
                    ProductOptionId = option.ProductOptionId,
                    ProductOptionValueId = option.ProductOptionValueId,
                    Name = option.OptionName,
                    Value = option.Value,
                    Type = "radio", // як у рядків розміру, які пише сайт; звірити з dd_option.type
                });
        }

        var sort = 0;
        void Total(string code, string title, decimal value) =>
            db.DdOrderTotals.Add(new DdOrderTotal { OrderId = row.OrderId, Code = code, Title = title, Value = value, SortOrder = sort++ });
        Total("sub_total", "Сума", order.Draft.SubTotal);
        Total("shipping", row.ShippingMethod, order.Draft.ShippingCost);
        if (order.Draft.Discount > 0)
            Total("coupon", "Знижка", -order.Draft.Discount);
        Total("total", "Всього", order.Draft.Total);

        await db.SaveChangesAsync(ct);
        return row.OrderId;
    }

    private static bool Same(string a, string b) => string.Equals(a.Trim(), b, StringComparison.OrdinalIgnoreCase);
}
