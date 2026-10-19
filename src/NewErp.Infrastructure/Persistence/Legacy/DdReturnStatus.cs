using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdReturnStatus
{
    public int ReturnStatusId { get; set; }

    public int LanguageId { get; set; }

    public string Name { get; set; } = null!;
}
