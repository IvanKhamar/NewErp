using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdOptionDescription
{
    public int OptionId { get; set; }

    public int LanguageId { get; set; }

    public string Name { get; set; } = null!;
}
