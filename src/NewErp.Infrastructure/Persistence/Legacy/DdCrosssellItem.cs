using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdCrosssellItem
{
    public int CrosssellItemId { get; set; }

    public string Type { get; set; } = null!;

    public int TargetId { get; set; }

    public decimal Discount { get; set; }
}
