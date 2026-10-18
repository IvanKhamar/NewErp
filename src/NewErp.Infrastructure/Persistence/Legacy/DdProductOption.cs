using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdProductOption
{
    public int ProductOptionId { get; set; }

    public int ProductId { get; set; }

    public int OptionId { get; set; }

    public string Value { get; set; } = null!;

    public bool Required { get; set; }
}
