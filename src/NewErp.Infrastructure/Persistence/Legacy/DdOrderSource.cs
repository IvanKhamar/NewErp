using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdOrderSource
{
    public int OrderSourceId { get; set; }

    public int LanguageId { get; set; }

    public string Name { get; set; } = null!;
}
