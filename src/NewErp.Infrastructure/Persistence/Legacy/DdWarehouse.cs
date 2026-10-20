using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdWarehouse
{
    public int WarehouseId { get; set; }

    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string Contact { get; set; } = null!;

    public string Telephone { get; set; } = null!;

    public string City { get; set; } = null!;

    public string Address { get; set; } = null!;

    public string Type { get; set; } = null!;

    public string Image { get; set; } = null!;
}
