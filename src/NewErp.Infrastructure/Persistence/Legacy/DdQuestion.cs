using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdQuestion
{
    public int QuestionId { get; set; }

    public int ProductId { get; set; }

    public string Name { get; set; } = null!;

    public string Telephone { get; set; } = null!;

    public string Question { get; set; } = null!;

    public DateTime DateAdded { get; set; }
}
