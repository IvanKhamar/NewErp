using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdProductLabel
{
    public int LabelId { get; set; }

    public string Name { get; set; } = null!;

    public string BackgroundColor { get; set; } = null!;

    public string TextColor { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool? Status { get; set; }
}
