namespace NewErp.Application.Stock;

public sealed record StockBalanceRow(int ItemId, int WarehouseId, decimal Quantity);

/// <summary>Місце, де лежить позиція. WarehouseId = null — основний склад.</summary>
public sealed record PlaceDto(int? WarehouseId, string Name, decimal Quantity);

/// <summary>Позиція з її загальним залишком і місцями: першим завжди основний склад, далі склади з ненульовим залишком.</summary>
public sealed record ItemPlacementDto(int ItemId, string Name, string Unit, decimal Total, IReadOnlyList<PlaceDto> Places);

/// <summary>null у From/To — основний склад.</summary>
public sealed record TransferStockRequest(int? FromWarehouseId, int? ToWarehouseId, decimal Quantity, string? Comment = null);

public sealed record StockTransferDto(
    int Id, int? FromWarehouseId, string From, int? ToWarehouseId, string To, decimal Quantity, string Comment, string Username, DateTime CreatedAt);

/// <summary>Стовпчик звіту: місце й вартість того, що там лежить (за ціною позиції).</summary>
public sealed record StockByWarehousePlace(int? WarehouseId, string Name, decimal Value);

/// <summary>Quantities — залишки в порядку стовпчиків Places.</summary>
public sealed record StockByWarehouseRow(int ItemId, string Name, string Unit, string? CategoryName, decimal Total, IReadOnlyList<decimal> Quantities);

/// <summary>Усі активні позиції за місцями: основний склад і лише ті склади, де щось лежить.</summary>
public sealed record StockByWarehouseDto(IReadOnlyList<StockByWarehousePlace> Places, IReadOnlyList<StockByWarehouseRow> Rows);
