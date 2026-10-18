using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdProductManualPin
{
    public int Id { get; set; }

    public string GroupType { get; set; } = null!;

    public string GroupKey { get; set; } = null!;

    public int Position { get; set; }

    public int ProductId { get; set; }

    public DateTime CreatedAt { get; set; }
}
