using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdProductDescription
{
    public int ProductId { get; set; }

    public int LanguageId { get; set; }

    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string Tag { get; set; } = null!;

    public string MetaTitle { get; set; } = null!;

    public string MetaDescription { get; set; } = null!;

    public string MetaKeyword { get; set; } = null!;
}
