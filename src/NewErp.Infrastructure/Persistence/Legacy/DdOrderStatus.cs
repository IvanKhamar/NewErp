using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdOrderStatus
{
    public int OrderStatusId { get; set; }

    public int LanguageId { get; set; }

    public string Name { get; set; } = null!;

    public string Color { get; set; } = null!;
}
