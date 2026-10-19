using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdReturnHistory
{
    public int ReturnHistoryId { get; set; }

    public int ReturnId { get; set; }

    public int ReturnStatusId { get; set; }

    public bool Notify { get; set; }

    public string Comment { get; set; } = null!;

    public DateTime DateAdded { get; set; }
}
