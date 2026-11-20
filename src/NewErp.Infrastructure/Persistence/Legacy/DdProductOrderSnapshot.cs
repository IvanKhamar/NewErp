using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdProductOrderSnapshot
{
    public int SnapshotId { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
}
