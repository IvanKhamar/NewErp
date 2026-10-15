using System.Reflection;

namespace NewErp.Web.Tests;

public class LayerDependencyTests
{
    /// <summary>Інтерфейс говорить з сервером лише через HTTP: жодного прямого доступу до бази чи коду API.</summary>
    [Fact]
    public void Web_DoesNotReferenceInfrastructureOrApi()
    {
        var references = typeof(App).Assembly.GetReferencedAssemblies().Select(a => a.Name);

        Assert.Empty(references.Intersect(["NewErp.Infrastructure", "NewErp.Api"]));
    }
}
