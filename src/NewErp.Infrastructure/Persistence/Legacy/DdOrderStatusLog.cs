using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdOrderStatusLog
{
    public int OrderStatusLogId { get; set; }

    public int OrderId { get; set; }

    public int UserId { get; set; }

    public int OldStatusId { get; set; }

    public int NewStatusId { get; set; }

    public DateTime DateAdded { get; set; }
}
