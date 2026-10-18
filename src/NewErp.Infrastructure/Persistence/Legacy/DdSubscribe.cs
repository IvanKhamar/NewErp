using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdSubscribe
{
    public int SubscribeId { get; set; }

    public string Email { get; set; } = null!;

    public DateTime DateAdded { get; set; }
}
