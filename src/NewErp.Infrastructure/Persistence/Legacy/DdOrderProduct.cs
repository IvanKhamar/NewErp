using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdOrderProduct
{
    public int OrderProductId { get; set; }

    public int OrderId { get; set; }

    public int ProductId { get; set; }

    public string Name { get; set; } = null!;

    public string Model { get; set; } = null!;

    public int Quantity { get; set; }

    public decimal Price { get; set; }

    public decimal Total { get; set; }

    public decimal Tax { get; set; }

    public int Reward { get; set; }
}