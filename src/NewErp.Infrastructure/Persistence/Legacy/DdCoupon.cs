using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdCoupon
{
    public int CouponId { get; set; }

    public string Name { get; set; } = null!;

    public string Code { get; set; } = null!;

    public string Type { get; set; } = null!;

    public decimal Discount { get; set; }

    public bool Logged { get; set; }

    public bool Shipping { get; set; }

    public decimal Total { get; set; }

    public DateTime DateStart { get; set; }

    public DateTime DateEnd { get; set; }

    public int UsesTotal { get; set; }

    public string UsesCustomer { get; set; } = null!;

    public bool Status { get; set; }

    public DateTime DateAdded { get; set; }
}
