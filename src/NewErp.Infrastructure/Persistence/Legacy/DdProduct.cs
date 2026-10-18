using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdProduct
{
    public int ProductId { get; set; }

    public string Model { get; set; } = null!;

    public string Sku { get; set; } = null!;

    public string Upc { get; set; } = null!;

    public string Ean { get; set; } = null!;

    public string Jan { get; set; } = null!;

    public string Isbn { get; set; } = null!;

    public string Mpn { get; set; } = null!;

    public string Location { get; set; } = null!;

    public int Quantity { get; set; }

    public int StockStatusId { get; set; }

    public string? Image { get; set; }

    public int ManufacturerId { get; set; }

    public bool? Shipping { get; set; }

    public decimal Price { get; set; }

    public int Points { get; set; }

    public int TaxClassId { get; set; }

    public DateTime DateAvailable { get; set; }

    public decimal Weight { get; set; }

    public int WeightClassId { get; set; }

    public decimal Length { get; set; }

    public decimal Width { get; set; }

    public decimal Height { get; set; }

    public int LengthClassId { get; set; }

    public bool? Subtract { get; set; }

    public int Minimum { get; set; }

    public int SortOrder { get; set; }

    public bool Status { get; set; }

    public int Viewed { get; set; }

    public DateTime DateAdded { get; set; }

    public DateTime DateModified { get; set; }
}
