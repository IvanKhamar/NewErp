using NewErp.Domain.Orders;

namespace NewErp.UnitTests.Domain;

public class SharedIpTests
{
    [Fact]
    public void From_TwoClients_IsNoWarning() =>
        Assert.Null(SharedIp.From("10.0.0.1", [("Ганна", "Нова", "0671112233"), ("Іван", "Сидоренко", "0672223344")]));

    [Fact]
    public void From_SameClientInAnotherForm_CountsOnce()
    {
        var shared = SharedIp.From("10.0.0.1",
        [
            ("Ганна", "Нова", "067-111-22-33"),
            (" ганна ", "НОВА", "0671112233"), // той самий клієнт
            ("Ганна", "Нова", "0509998877"), // тезка з іншим телефоном — інший клієнт
            ("Іван", "Сидоренко", "0672223344"),
        ]);

        Assert.Equal(3, shared!.UniqueClients);
        Assert.Equal(["Ганна Нова (067-111-22-33)", "Ганна Нова (0509998877)", "Іван Сидоренко (0672223344)"], shared.Clients);
    }

    [Fact]
    public void From_ListsAtMostTen_ButCountsAll()
    {
        var orders = Enumerable.Range(10, 15).Select(i => ("Клієнт", $"№{i}", $"06700000{i}"));

        var shared = SharedIp.From("10.0.0.1", orders);

        Assert.Equal((15, SharedIp.Listed), (shared!.UniqueClients, shared.Clients.Count));
    }
}
