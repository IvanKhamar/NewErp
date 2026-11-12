using System.Net.Http.Headers;
using System.Net.Http.Json;

using NewErp.Application.Catalog;
using NewErp.Application.Common;

namespace NewErp.Web.Api;

/// <summary>Виклики API товарів магазину (див. <see cref="StockApi"/> про те, навіщо окремий клас).</summary>
public sealed class ProductsApi(HttpClient http)
{
    public Task<PagedResult<ProductListItem>> ListAsync(ProductFilter filter)
    {
        var query = new List<string> { $"page={filter.Page}", $"pageSize={filter.PageSize}" };
        if (filter.CategoryId is { } category) query.Add($"categoryId={category}");
        if (filter.LabelId is { } label) query.Add($"labelId={label}");
        if (filter.Enabled is { } enabled) query.Add($"enabled={(enabled ? "true" : "false")}");
        if (!string.IsNullOrWhiteSpace(filter.Search)) query.Add($"search={Uri.EscapeDataString(filter.Search.Trim())}");
        return GetAsync<PagedResult<ProductListItem>>("/catalog/products?" + string.Join('&', query));
    }

    public Task<ProductCardDto> GetAsync(int id) => GetAsync<ProductCardDto>($"/catalog/products/{id}");

    public async Task<ProductCardDto> UpdateAsync(int id, UpdateProductRequest request) =>
        await ReadAsync<ProductCardDto>(await http.PatchAsJsonAsync($"/catalog/products/{id}", request));

    public Task<IReadOnlyList<LookupItem>> LabelsAsync() => GetAsync<IReadOnlyList<LookupItem>>("/catalog/products/labels");

    public Task<IReadOnlyList<LookupItem>> CategoriesAsync() => GetAsync<IReadOnlyList<LookupItem>>("/catalog/products/categories");

    // ═══ Уся форма товару (пункти 43, 45, 76) ═══

    public Task<ProductFormLookups> FormLookupsAsync() => GetAsync<ProductFormLookups>("/catalog/products/form-lookups");

    public Task<ProductEditDto> GetEditAsync(int id) => GetAsync<ProductEditDto>($"/catalog/products/{id}/edit");

    public async Task<CreatedProductDto> CreateAsync(SaveProductRequest request) =>
        await ReadAsync<CreatedProductDto>(await http.PostAsJsonAsync("/catalog/products", request));

    public async Task<ProductEditDto> SaveAsync(int id, SaveProductRequest request) =>
        await ReadAsync<ProductEditDto>(await http.PutAsJsonAsync($"/catalog/products/{id}", request));

    public async Task<CreatedProductDto> CopyAsync(int id) =>
        await ReadAsync<CreatedProductDto>(await http.PostAsync($"/catalog/products/{id}/copy", null));

    public async Task<RemoveNewResult> RemoveNewAsync(string sku) =>
        await ReadAsync<RemoveNewResult>(await http.PostAsJsonAsync("/catalog/products/remove-new", new RemoveNewRequest(sku)));

    // ═══ Картинки (пункт 75) й імпорт CSV (пункт 44) ═══

    public async Task<PictureDto> UploadPictureAsync(string kind, Stream file, string fileName) =>
        await ReadAsync<PictureDto>(await http.PostAsync($"/catalog/pictures/{kind}", FileForm(file, fileName)));

    public async Task<ImportPreview> PreviewImportAsync(Stream file, string fileName) =>
        await ReadAsync<ImportPreview>(await http.PostAsync("/catalog/products/import/preview", FileForm(file, fileName)));

    public async Task<ImportResult> ImportAsync(Stream file, string fileName) =>
        await ReadAsync<ImportResult>(await http.PostAsync("/catalog/products/import", FileForm(file, fileName)));

    private static MultipartFormDataContent FileForm(Stream file, string fileName)
    {
        var content = new StreamContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new MultipartFormDataContent { { content, "file", fileName } };
    }

    private async Task<T> GetAsync<T>(string url) => await ReadAsync<T>(await http.GetAsync(url));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new ApiException(await ApiErrors.ReadAsync(response));
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
