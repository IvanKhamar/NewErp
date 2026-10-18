using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdSetting
{
    public int SettingId { get; set; }

    public int StoreId { get; set; }

    public string Code { get; set; } = null!;

    public string Key { get; set; } = null!;

    public string Value { get; set; } = null!;

    public bool Serialized { get; set; }
}
