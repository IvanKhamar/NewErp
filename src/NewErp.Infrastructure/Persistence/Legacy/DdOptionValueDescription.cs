using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdOptionValueDescription
{
    public int OptionValueId { get; set; }

    public int LanguageId { get; set; }

    public int OptionId { get; set; }

    public string Name { get; set; } = null!;
}
