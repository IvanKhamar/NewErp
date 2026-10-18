using System.Globalization;
using System.Text;
using System.Xml;

using Microsoft.Extensions.Options;

using NewErp.Application.Common;
using NewErp.Domain.Common;
using NewErp.Domain.Partners;

namespace NewErp.Application.Partners;

/// <summary>Партнери-перепродавці: налаштування, відповідність категорій і товарів, XML-фід для кожного партнера.</summary>
public sealed class PartnerService(IPartnerRepository repository, IOptions<PartnerFeedOptions> options, TimeProvider clock)
{
    private const int SearchLimit = 20;

    public async Task<int> CreateAsync(SavePartnerRequest request, CancellationToken ct)
    {
        var partner = new Partner(ToSettings(request), Now());
        Apply(partner, request);
        await EnsureUniqueCodeAsync(partner.Code, null, ct);
        repository.Add(partner);
        await repository.SaveChangesAsync(ct);
        return partner.Id;
    }

    public async Task UpdateAsync(int id, SavePartnerRequest request, CancellationToken ct)
    {
        var partner = await repository.GetAsync(id, ct) ?? throw new NotFoundException($"Партнера {id} не знайдено.");
        partner.Update(ToSettings(request), Now());
        Apply(partner, request);
        await EnsureUniqueCodeAsync(partner.Code, id, ct);
        await repository.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var partner = await repository.GetAsync(id, ct) ?? throw new NotFoundException($"Партнера {id} не знайдено.");
        repository.Remove(partner);
        await repository.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<PartnerProductPickDto>> SearchProductsAsync(string? term, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(term)
            ? Task.FromResult<IReadOnlyList<PartnerProductPickDto>>([])
            : repository.SearchProductsAsync(term.Trim(), SearchLimit, ct);

    /// <summary>XML-фід партнера у форматі YML (yml_catalog), як віддавав старий сайт. null — партнера немає або фід вимкнено.</summary>
    public async Task<string?> BuildFeedXmlAsync(string code, CancellationToken ct)
    {
        var partner = await repository.GetByCodeAsync(code, ct);
        if (partner is null || !partner.FeedEnabled)
            return null;

        var products = await repository.GetFeedProductsAsync(
            partner.CategoryMap.Select(r => r.CategoryId).Distinct().ToList(),
            partner.ProductMap.Select(r => r.ProductId).Distinct().ToList(), ct);
        var categoryIds = partner.CategoryMap.Select(r => r.CategoryId).Concat(products.SelectMany(p => p.CategoryIds)).Distinct().ToList();
        var categoryNames = await repository.GetCategoryNamesAsync(categoryIds, ct);

        var storeUrl = options.Value.StoreUrl.Trim();
        if (storeUrl != "" && !storeUrl.EndsWith('/'))
            storeUrl += "/";
        var feed = PartnerFeedBuilder.Build(partner, products, categoryNames, storeUrl);
        return WriteXml(feed, partner, options.Value, storeUrl, Now());
    }

    private static string WriteXml(PartnerFeed feed, Partner partner, PartnerFeedOptions shop, string storeUrl, DateTime now)
    {
        var output = new StringBuilder();
        // XmlWriter сам екранує текст і атрибути; StringWriter з UTF-8 потрібен, щоб у заголовку стояло encoding="utf-8".
        using (var writer = XmlWriter.Create(new Utf8StringWriter(output), new XmlWriterSettings { Indent = true }))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("yml_catalog");
            writer.WriteAttributeString("date", now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            writer.WriteStartElement("shop");
            writer.WriteElementString("name", shop.ShopName);
            writer.WriteElementString("company", shop.Company);
            writer.WriteElementString("url", storeUrl);

            writer.WriteStartElement("currencies");
            writer.WriteStartElement("currency");
            writer.WriteAttributeString("id", "UAH");
            writer.WriteAttributeString("rate", "1");
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteStartElement("categories");
            foreach (var category in feed.Categories)
            {
                writer.WriteStartElement("category");
                writer.WriteAttributeString("id", category.Id);
                // Атрибут названо за першим партнером (EVA); його фід уже читає саме таку назву.
                if (category.ExternalId is not null)
                    writer.WriteAttributeString("eva_id", category.ExternalId);
                writer.WriteString(category.Name);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteStartElement("offers");
            foreach (var offer in feed.Offers)
            {
                writer.WriteStartElement("offer");
                writer.WriteAttributeString("id", offer.Id);
                writer.WriteAttributeString("available", "true");
                writer.WriteElementString("price", Money(offer.Price));
                if (offer.PriceOld is { } priceOld)
                    writer.WriteElementString("price_old", Money(priceOld));
                writer.WriteElementString("stock_quantity", partner.DefaultQuantity.ToString(CultureInfo.InvariantCulture));
                writer.WriteElementString("currencyId", "UAH");
                writer.WriteElementString("categoryId", offer.CategoryId);
                foreach (var picture in offer.Pictures)
                    writer.WriteElementString("picture", picture);
                writer.WriteElementString("vendor", partner.VendorName);
                if (offer.Model != "")
                    writer.WriteElementString("article", offer.Model);
                if (offer.VendorCode is not null)
                    writer.WriteElementString("vendorCode", offer.VendorCode);
                writer.WriteElementString("name_ua", offer.Name);
                writer.WriteStartElement("description_ua");
                writer.WriteCData(offer.Description);
                writer.WriteEndElement();
                if (offer.Size is not null)
                    WriteParam(writer, "Розмір", offer.Size);
                WriteParam(writer, "Штрихкод", offer.Barcode);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }
        return output.ToString();
    }

    private static void WriteParam(XmlWriter writer, string name, string value)
    {
        writer.WriteStartElement("param");
        writer.WriteAttributeString("name", name);
        writer.WriteString(value);
        writer.WriteEndElement();
    }

    // Як PHP виводить число: без зайвих нулів (1250, 1250.5).
    private static string Money(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private async Task EnsureUniqueCodeAsync(string code, int? exceptId, CancellationToken ct)
    {
        if (await repository.CodeExistsAsync(code, exceptId, ct))
            throw new DomainException($"Партнер з кодом «{code}» уже є.");
    }

    private static void Apply(Partner partner, SavePartnerRequest request)
    {
        partner.SetCategoryMap((request.CategoryMap ?? []).Select(r =>
            new CategoryMapping(r.CategoryId, r.SizeName, r.ExternalId, r.ExternalName, r.PercentSupply, r.PercentSale)));
        partner.SetProductMap((request.ProductMap ?? []).Select(r =>
            new ProductMapping(r.ProductId, r.SizeName, r.ExternalId, r.ExternalName, r.PriceSupply, r.PriceSale)));
    }

    private static PartnerSettings ToSettings(SavePartnerRequest r) =>
        new(r.Code, r.Name, r.IsActive, r.FeedUrl, r.FeedEnabled, r.VendorName, r.Notes, r.DefaultQuantity);

    private DateTime Now() => clock.GetLocalNow().DateTime;

    private sealed class Utf8StringWriter(StringBuilder builder) : StringWriter(builder, CultureInfo.InvariantCulture)
    {
        public override Encoding Encoding => new UTF8Encoding(false);
    }
}
