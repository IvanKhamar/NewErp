using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdReturn
{
    public int ReturnId { get; set; }

    public int OrderId { get; set; }

    public int ProductId { get; set; }

    public int CustomerId { get; set; }

    public string Firstname { get; set; } = null!;

    public string Lastname { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Telephone { get; set; } = null!;

    public string Product { get; set; } = null!;

    public string Model { get; set; } = null!;

    public int Quantity { get; set; }

    public bool Opened { get; set; }

    public int ReturnReasonId { get; set; }

    public int ReturnActionId { get; set; }

    public int ReturnStatusId { get; set; }

    public string? Comment { get; set; }

    public DateTime DateOrdered { get; set; }

    public DateTime DateAdded { get; set; }

    public DateTime DateModified { get; set; }
}
