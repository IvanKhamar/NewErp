using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdProductOrderSnapshotItem
{
    public int SnapshotId { get; set; }

    public int ProductId { get; set; }

    public int SortOrder { get; set; }
}
