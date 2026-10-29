using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdVoucherTheme
{
    public int VoucherThemeId { get; set; }

    public string Image { get; set; } = null!;
}
