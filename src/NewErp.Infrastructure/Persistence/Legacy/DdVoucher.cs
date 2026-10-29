using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdVoucher
{
    public int VoucherId { get; set; }

    public int OrderId { get; set; }

    public string Code { get; set; } = null!;

    public string FromName { get; set; } = null!;

    public string FromEmail { get; set; } = null!;

    public string ToName { get; set; } = null!;

    public string ToEmail { get; set; } = null!;

    public int VoucherThemeId { get; set; }

    public string Message { get; set; } = null!;

    public decimal Amount { get; set; }

    public bool Status { get; set; }

    public DateTime DateAdded { get; set; }
}
