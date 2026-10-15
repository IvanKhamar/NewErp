namespace NewErp.Domain.Production;

/// <summary>Правило специфікацій зі старого модуля: рядок для конкретного розміру перекриває загальний рядок (без розміру).</summary>
public static class BomResolver
{
    /// <summary>
    /// Діючі рядки для розміру: по одному на ключ (розхідник або деталь).
    /// Якщо для ключа є рядок саме цього розміру, береться він, інакше — загальний.
    /// </summary>
    public static IReadOnlyList<T> Resolve<T>(IEnumerable<T> rows, int sizeId, Func<T, int?> sizeOf, Func<T, int?> keyOf)
    {
        var effective = new Dictionary<int, T>();
        foreach (var row in rows)
        {
            if (keyOf(row) is not { } key)
                continue;
            if (sizeOf(row) == sizeId)
                effective[key] = row;
            else if (sizeOf(row) is null)
                effective.TryAdd(key, row);
        }
        return effective.Values.ToList();
    }
}
