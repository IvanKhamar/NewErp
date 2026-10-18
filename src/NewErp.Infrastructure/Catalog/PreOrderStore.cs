using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Catalog;
using NewErp.Domain.Catalog;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Catalog;

public sealed class PreOrderStore(ErpDbContext db, IOptions<ErpOptions> options) : IPreOrderStore
{
    private readonly int _languageId = options.Value.LanguageId;

    public async Task<IReadOnlyList<PreOrderRow>> ListAsync(PreOrderFilter filter, int limit, CancellationToken ct)
    {
        var search = filter.Search;
        var rows = await (
            from v in db.DdProductOptionValues
            join p in db.DdProducts on v.ProductId equals p.ProductId
            join d in db.DdProductDescriptions on new { p.ProductId, LanguageId = _languageId } equals new { d.ProductId, d.LanguageId }
            join vd in db.DdOptionValueDescriptions on new { v.OptionValueId, LanguageId = _languageId } equals new { vd.OptionValueId, vd.LanguageId } into vds
            from vd in vds.DefaultIfEmpty()
            where (search == null || d.Name.Contains(search) || p.Model.Contains(search))
                && (!filter.OnlyWithQuota || v.PreOrderQuantity > 0)
            orderby d.Name, v.ProductId, v.ProductOptionValueId
            // Без date_available: '0000-00-00' DateTime не прочитає, дату беремо нижче текстом.
            select new { v.ProductOptionValueId, v.ProductId, d.Name, p.Model, Size = vd == null ? "" : vd.Name, v.Quantity, v.PreOrderQuantity, v.Subtract })
            .Take(limit)
            .ToListAsync(ct);
        var dates = await DatesAsync(rows.Select(r => r.ProductOptionValueId).ToList(), ct);

        return rows.Select(r => new PreOrderRow(r.ProductOptionValueId, r.ProductId, r.Name, r.Model, r.Size, r.Quantity, r.PreOrderQuantity,
                dates.TryGetValue(r.ProductOptionValueId, out var date) ? date : null, PreOrder.Availability(r.Quantity, r.PreOrderQuantity, r.Subtract)))
            .ToList();
    }

    public async Task<bool> UpdateAsync(int productOptionValueId, int quota, DateOnly? dateAvailable, CancellationToken ct)
    {
        var date = dateAvailable?.ToDateTime(TimeOnly.MinValue);
        return await db.DdProductOptionValues.Where(v => v.ProductOptionValueId == productOptionValueId)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.PreOrderQuantity, quota).SetProperty(v => v.DateAvailable, date), ct) > 0;
    }

    public async Task<int> SettleAsync(IReadOnlyCollection<int>? productIds, CancellationToken ct)
    {
        var rows = await db.DdProductOptionValues.AsNoTracking()
            .Where(v => v.Quantity < 0 && v.PreOrderQuantity > 0 && (productIds == null || productIds.Contains(v.ProductId)))
            .Select(v => new { v.ProductOptionValueId, v.Quantity, v.PreOrderQuantity })
            .ToListAsync(ct);
        // MyISAM без транзакцій, тож кожен розмір — окремим UPDATE з умовою на старі значення: якщо вітрина тим часом
        // продала ще, рядок не зміниться й буде списаний наступного разу.
        var changed = 0;
        foreach (var row in rows)
        {
            var (quantity, quota) = PreOrder.Settle(row.Quantity, row.PreOrderQuantity);
            changed += await db.DdProductOptionValues
                .Where(v => v.ProductOptionValueId == row.ProductOptionValueId && v.Quantity == row.Quantity && v.PreOrderQuantity == row.PreOrderQuantity)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.Quantity, quantity).SetProperty(v => v.PreOrderQuantity, quota), ct);
        }
        return changed;
    }

    private async Task<Dictionary<int, DateOnly>> DatesAsync(List<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
            return [];
        // У SQL ідуть лише числа.
        var sql = "SELECT product_option_value_id AS Id, CAST(date_available AS CHAR) AS Text FROM dd_product_option_value WHERE product_option_value_id IN ("
            + string.Join(',', ids) + ")";
        var rows = await db.Database.SqlQueryRaw<DateText>(sql).ToListAsync(ct);
        var dates = new Dictionary<int, DateOnly>();
        foreach (var row in rows)
            if (DateOnly.TryParseExact(row.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && date.Year > 1)
                dates[row.Id] = date;
        return dates;
    }

    private sealed record DateText(int Id, string? Text);
}
