using NewErp.Application.Stock;

namespace NewErp.Application.Production;

public sealed record SewnBalanceRow(int SizeId, int WarehouseId, decimal Quantity);

public sealed record ActiveSizeRow(int ProductionId, string ModelName, int SizeId, string SizeName, decimal Sewn);

/// <summary>Розмір моделі з пошитими виробами за місцями: першим основний склад, далі склади з ненульовим залишком.</summary>
public sealed record SizePlacesDto(int SizeId, string SizeName, decimal Sewn, IReadOnlyList<PlaceDto> Places);

/// <summary>null у From/To — основний склад.</summary>
public sealed record TransferSewnRequest(int SizeId, int? FromWarehouseId, int? ToWarehouseId, decimal Quantity, string? Comment = null);

public sealed record SewnTransferDto(
    int Id, int SizeId, string SizeName, int? FromWarehouseId, string From, int? ToWarehouseId, string To,
    decimal Quantity, string Comment, string Username, DateTime CreatedAt);

public sealed record SewnByWarehousePlace(int? WarehouseId, string Name);

/// <summary>Quantities — у порядку стовпчиків Places.</summary>
public sealed record SewnByWarehouseRow(int ProductionId, string ModelName, int SizeId, string SizeName, decimal Total, IReadOnlyList<decimal> Quantities);

/// <summary>Розміри активних моделей за місцями: основний склад і лише ті склади, де щось лежить.</summary>
public sealed record SewnByWarehouseDto(IReadOnlyList<SewnByWarehousePlace> Places, IReadOnlyList<SewnByWarehouseRow> Rows);
