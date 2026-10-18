using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdFilter
{
    public int FilterId { get; set; }

    public int FilterGroupId { get; set; }

    public int SortOrder { get; set; }
}
