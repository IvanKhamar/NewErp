using System;
using System.Collections.Generic;

namespace NewErp.Infrastructure.Persistence.Legacy;

public partial class DdOrder
{
    public int OrderId { get; set; }

    public int InvoiceNo { get; set; }

    public string InvoicePrefix { get; set; } = null!;

    public int StoreId { get; set; }

    public string StoreName { get; set; } = null!;

    public string StoreUrl { get; set; } = null!;

    public int CustomerId { get; set; }

    public int CustomerGroupId { get; set; }

    public string Firstname { get; set; } = null!;

    public string Lastname { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Telephone { get; set; } = null!;

    public string Fax { get; set; } = null!;

    public string CustomField { get; set; } = null!;

    public string PaymentFirstname { get; set; } = null!;

    public string PaymentLastname { get; set; } = null!;

    public string PaymentCompany { get; set; } = null!;

    public string PaymentAddress1 { get; set; } = null!;

    public string PaymentAddress2 { get; set; } = null!;

    public string PaymentCity { get; set; } = null!;

    public string PaymentPostcode { get; set; } = null!;

    public string PaymentCountry { get; set; } = null!;

    public int PaymentCountryId { get; set; }

    public string PaymentZone { get; set; } = null!;

    public int PaymentZoneId { get; set; }

    public string PaymentAddressFormat { get; set; } = null!;

    public string PaymentCustomField { get; set; } = null!;

    public string PaymentMethod { get; set; } = null!;

    public string PaymentCode { get; set; } = null!;

    public string ShippingFirstname { get; set; } = null!;

    public string ShippingLastname { get; set; } = null!;

    public string ShippingCompany { get; set; } = null!;

    public string ShippingAddress1 { get; set; } = null!;

    public string ShippingAddress2 { get; set; } = null!;

    public string ShippingCity { get; set; } = null!;

    public string ShippingPostcode { get; set; } = null!;

    public string ShippingCountry { get; set; } = null!;

    public int ShippingCountryId { get; set; }

    public string ShippingZone { get; set; } = null!;

    public int ShippingZoneId { get; set; }

    public string ShippingAddressFormat { get; set; } = null!;

    public string ShippingCustomField { get; set; } = null!;

    public string ShippingMethod { get; set; } = null!;

    public string ShippingCode { get; set; } = null!;

    public string Comment { get; set; } = null!;

    public decimal Total { get; set; }

    public int OrderStatusId { get; set; }

    public int AffiliateId { get; set; }

    public decimal Commission { get; set; }

    public int MarketingId { get; set; }

    public string Tracking { get; set; } = null!;

    public int LanguageId { get; set; }

    public int CurrencyId { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public decimal CurrencyValue { get; set; }

    public string Ip { get; set; } = null!;

    public string ForwardedIp { get; set; } = null!;

    public string UserAgent { get; set; } = null!;

    public string AcceptLanguage { get; set; } = null!;

    public DateTime DateAdded { get; set; }

    public DateTime DateModified { get; set; }

    public int OrderSourceId { get; set; }
}