namespace NewErp.Domain.Catalog;

/// <summary>Акційна ціна старого сайту: фіксована ціна або відсоток від ціни товару, округлений до десятків.</summary>
public sealed record SpecialPrice(decimal Price, decimal Percent)
{
    public decimal Apply(decimal regular)
    {
        if (Percent <= 0)
            return Price;
        // round(price * (100 - percent) / 100 / 10) * 10, як у PHP: половини від нуля.
        var tens = Math.Round(regular * (100 - Percent) / 100 / 10, MidpointRounding.AwayFromZero);
        return Math.Max(tens * 10, 0);
    }
}
