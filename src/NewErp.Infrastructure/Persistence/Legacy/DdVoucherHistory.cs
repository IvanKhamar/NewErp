using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdVoucherHistory
{
    public int VoucherHistoryId { get; set; }

    public int VoucherId { get; set; }

    public int OrderId { get; set; }

    public decimal Amount { get; set; }

    public DateTime DateAdded { get; set; }
}
