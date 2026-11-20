using System.Net.Http.Json;

using NewErp.Application.Catalog;
using NewErp.Application.Common;

namespace NewErp.Web.Api;

/// <summary>Виклики API довідників каталогу старої адмінки (Python first, група 8): мітки, пресети, категорії.</summary>
public sealed class CatalogApi(HttpClient http)
{
    // ═══ Мітки й пресети пошуку (пункт 47) ═══

    public Task<List<LabelDto>> LabelsAsync() => GetAsync<List<LabelDto>>("/catalog/labels");

    public async Task<LabelDto> SaveLabelAsync(LabelDto label) => label.Id == 0
        ? await ReadAsync<LabelDto>(await http.PostAsJsonAsync("/catalog/labels", label))
        : await ReadAsync<LabelDto>(await http.PutAsJsonAsync($"/catalog/labels/{label.Id}", label));

    public async Task DeleteLabelAsync(int id) => await EnsureAsync(await http.DeleteAsync($"/catalog/labels/{id}"));

    public Task<List<SearchPresetDto>> PresetsAsync() => GetAsync<List<SearchPresetDto>>("/catalog/search-presets");

    public async Task<SearchPresetDto> SavePresetAsync(SearchPresetDto preset) => preset.Id == 0
        ? await ReadAsync<SearchPresetDto>(await http.PostAsJsonAsync("/catalog/search-presets", preset))
        : await ReadAsync<SearchPresetDto>(await http.PutAsJsonAsync($"/catalog/search-presets/{preset.Id}", preset));

    public async Task DeletePresetAsync(int id) => await EnsureAsync(await http.DeleteAsync($"/catalog/search-presets/{id}"));

    // ═══ Категорії (пункт 48) ═══

    public Task<List<CategoryListItem>> CategoriesAsync() => GetAsync<List<CategoryListItem>>("/catalog/categories");

    public Task<List<LookupItem>> CategoryFiltersAsync() => GetAsync<List<LookupItem>>("/catalog/categories/filters");

    public Task<CategoryEditDto> CategoryAsync(int id) => GetAsync<CategoryEditDto>($"/catalog/categories/{id}");

    public async Task<CategoryEditDto> SaveCategoryAsync(int? id, CategoryFormDto form) => id is { } existing
        ? await ReadAsync<CategoryEditDto>(await http.PutAsJsonAsync($"/catalog/categories/{existing}", form))
        : await ReadAsync<CategoryEditDto>(await http.PostAsJsonAsync("/catalog/categories", form));

    public async Task DeleteCategoryAsync(int id) => await EnsureAsync(await http.DeleteAsync($"/catalog/categories/{id}"));

    public async Task<CategoryRepairResult> RepairCategoriesAsync() =>
        await ReadAsync<CategoryRepairResult>(await http.PostAsync("/catalog/categories/repair", null));

    // ═══ Фільтри, атрибути, опції, виробники, завантаження (пункт 49) ═══

    public Task<List<ReferenceGroupDto>> GroupsAsync(string kind) => GetAsync<List<ReferenceGroupDto>>($"/catalog/references/{kind}");

    public async Task<ReferenceGroupDto> SaveGroupAsync(string kind, ReferenceGroupDto group) => group.Id == 0
        ? await ReadAsync<ReferenceGroupDto>(await http.PostAsJsonAsync($"/catalog/references/{kind}", group))
        : await ReadAsync<ReferenceGroupDto>(await http.PutAsJsonAsync($"/catalog/references/{kind}/{group.Id}", group));

    public async Task DeleteGroupAsync(string kind, int groupId) => await EnsureAsync(await http.DeleteAsync($"/catalog/references/{kind}/{groupId}"));

    public async Task<ReferenceGroupDto> SaveValueAsync(string kind, int groupId, ReferenceItemDto value) => value.Id == 0
        ? await ReadAsync<ReferenceGroupDto>(await http.PostAsJsonAsync($"/catalog/references/{kind}/{groupId}/values", value))
        : await ReadAsync<ReferenceGroupDto>(await http.PutAsJsonAsync($"/catalog/references/{kind}/{groupId}/values/{value.Id}", value));

    public async Task DeleteValueAsync(string kind, int valueId) => await EnsureAsync(await http.DeleteAsync($"/catalog/references/{kind}/values/{valueId}"));

    public Task<List<ManufacturerDto>> ManufacturersAsync() => GetAsync<List<ManufacturerDto>>("/catalog/manufacturers");

    public async Task<ManufacturerDto> SaveManufacturerAsync(ManufacturerDto manufacturer) => manufacturer.Id == 0
        ? await ReadAsync<ManufacturerDto>(await http.PostAsJsonAsync("/catalog/manufacturers", manufacturer))
        : await ReadAsync<ManufacturerDto>(await http.PutAsJsonAsync($"/catalog/manufacturers/{manufacturer.Id}", manufacturer));

    public async Task DeleteManufacturerAsync(int id) => await EnsureAsync(await http.DeleteAsync($"/catalog/manufacturers/{id}"));

    public Task<List<DownloadDto>> DownloadsAsync() => GetAsync<List<DownloadDto>>("/catalog/downloads");

    public async Task<DownloadDto> SaveDownloadAsync(DownloadDto download) => download.Id == 0
        ? await ReadAsync<DownloadDto>(await http.PostAsJsonAsync("/catalog/downloads", download))
        : await ReadAsync<DownloadDto>(await http.PutAsJsonAsync($"/catalog/downloads/{download.Id}", download));

    public async Task DeleteDownloadAsync(int id) => await EnsureAsync(await http.DeleteAsync($"/catalog/downloads/{id}"));

    private async Task<T> GetAsync<T>(string url) => await ReadAsync<T>(await http.GetAsync(url));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        await EnsureAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task EnsureAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new ApiException(await ApiErrors.ReadAsync(response));
    }
}
