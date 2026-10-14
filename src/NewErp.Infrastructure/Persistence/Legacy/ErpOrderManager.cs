using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class ErpOrderManager
{
    public int OrderId { get; set; }

    public int UserId { get; set; }

    public int AssignedByUserId { get; set; }

    public DateTime AssignedAt { get; set; }
}