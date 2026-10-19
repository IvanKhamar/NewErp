using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdCustomer
{
    public int CustomerId { get; set; }

    public int CustomerGroupId { get; set; }

    public int StoreId { get; set; }

    public int LanguageId { get; set; }

    public string Firstname { get; set; } = null!;

    public string Lastname { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Telephone { get; set; } = null!;

    public string Fax { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string Salt { get; set; } = null!;

    public string? Cart { get; set; }

    public string? Wishlist { get; set; }

    public bool Newsletter { get; set; }

    public int AddressId { get; set; }

    public string CustomField { get; set; } = null!;

    public string Ip { get; set; } = null!;

    public bool Status { get; set; }

    public bool Safe { get; set; }

    public string Token { get; set; } = null!;

    public string Code { get; set; } = null!;

    public DateTime DateAdded { get; set; }
}
