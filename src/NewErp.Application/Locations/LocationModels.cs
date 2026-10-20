namespace NewErp.Application.Locations;

public sealed record LocationDto(int Id, string Name, string Description, string Contact, string Telephone, string City, string Address, string Type);

public sealed record SaveLocationRequest(
    string? Name, string? Description = null, string? Contact = null, string? Telephone = null, string? City = null,
    string? Address = null, string? Type = null);

/// <summary>Підсумок імпорту: скільки додано й оновлено, або помилки (тоді нічого не записано).</summary>
public sealed record LocationImportResult(int Added, int Updated, IReadOnlyList<string> Errors);
