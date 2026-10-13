using System.Reflection;

namespace NewErp.UnitTests;

public class LayerDependencyTests
{
    [Theory]
    [InlineData("NewErp.Domain", new[] { "NewErp.Application", "NewErp.Infrastructure", "NewErp.Api" })]
    [InlineData("NewErp.Application", new[] { "NewErp.Infrastructure", "NewErp.Api" })]
    public void Layer_DoesNotDependOnOuterLayers(string layer, string[] forbidden)
    {
        var references = Assembly.Load(layer)
            .GetReferencedAssemblies()
            .Select(a => a.Name);

        Assert.Empty(references.Intersect(forbidden));
    }
}
