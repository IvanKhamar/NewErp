using NewErp.Domain.Catalog;
using NewErp.Domain.Common;

namespace NewErp.UnitTests.Domain;

/// <summary>Перенесено з Java ProductChangeTest.</summary>
public class ProductChangeTests
{
    private readonly HashSet<int> _labels = [1, 2];
    private readonly HashSet<int> _categories = [30, 31];

    [Fact]
    public void NullParts_StayAsTheyAre() =>
        Assert.Equal(new ProductChange(null, null, null, null), ProductChange.Of(null, null, null, null, _labels, _categories));

    [Fact]
    public void Price_IsNotNegative_WithTwoDecimals()
    {
        Assert.Equal(1499.9m, ProductChange.Of(1499.90m, null, null, null, _labels, _categories).Price);
        Assert.Equal(1500m, ProductChange.Of(1500.0000m, null, null, null, _labels, _categories).Price);
        Assert.Equal("Ціна — не менше 0, не більше двох знаків після коми.",
            Assert.Throws<DomainException>(() => ProductChange.Of(-1m, null, null, null, _labels, _categories)).Message);
        Assert.Throws<DomainException>(() => ProductChange.Of(10.005m, null, null, null, _labels, _categories));
    }

    [Fact]
    public void LabelsAndCategories_MustBeKnown_AndCategoriesNotEmpty()
    {
        var change = ProductChange.Of(null, false, [2, 2], [31], _labels, _categories);
        Assert.Equal([2], change.LabelIds!);
        Assert.Equal([31], change.CategoryIds!);
        Assert.Empty(ProductChange.Of(null, null, [], null, _labels, _categories).LabelIds!);

        Assert.Equal("Такої мітки немає.", Assert.Throws<DomainException>(() => ProductChange.Of(null, null, [9], null, _labels, _categories)).Message);
        Assert.Equal("Оберіть хоча б одну категорію.",
            Assert.Throws<DomainException>(() => ProductChange.Of(null, null, null, [], _labels, _categories)).Message);
        Assert.Equal("Такої категорії немає або вона вимкнена.",
            Assert.Throws<DomainException>(() => ProductChange.Of(null, null, null, [32], _labels, _categories)).Message);
    }
}
