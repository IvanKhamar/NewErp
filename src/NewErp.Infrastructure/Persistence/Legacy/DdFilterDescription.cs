using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdFilterDescription
{
    public int FilterId { get; set; }

    public int LanguageId { get; set; }

    public int FilterGroupId { get; set; }

    public string Name { get; set; } = null!;
}
