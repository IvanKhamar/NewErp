using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdProductImage
{
    public int ProductImageId { get; set; }

    public int ProductId { get; set; }

    public string? Image { get; set; }

    public int SortOrder { get; set; }
}
