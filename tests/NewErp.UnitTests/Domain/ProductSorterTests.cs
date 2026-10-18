using NewErp.Domain.Catalog;

namespace NewErp.UnitTests.Domain;

public class ProductSorterTests
{
    [Fact]
    public void BySales_PutsBestSellersFirst_AndKeepsOriginalOrderForTies()
    {
        var sales = new Dictionary<int, int> { [3] = 5, [4] = 9, [5] = 5 };

        Assert.Equal([4, 3, 5, 1, 2], ProductSorter.BySales([1, 2, 3, 4, 5], sales));
    }

    [Fact]
    public void Merge_PutsPinsOnTheirPositions_AndFillsTheRestFromQueue()
    {
        var result = ProductSorter.Merge([10, 20, 30, 40], [new SortPin(1, 30), new SortPin(3, 99)]);

        Assert.Equal([30, 10, 99, 20, 40], result);
    }

    [Fact]
    public void Merge_KeepsPinsBeyondTheListAtTheEnd()
    {
        var result = ProductSorter.Merge([10, 20], [new SortPin(10, 7), new SortPin(5, 8)]);

        Assert.Equal([10, 20, 8, 7], result);
    }

    [Fact]
    public void SortGroup_TypeCodeRoundTrips()
    {
        foreach (var type in Enum.GetValues<SortGroupType>())
            Assert.Equal(type, SortGroup.CodeToType(SortGroup.TypeToCode(type)));
    }
}
