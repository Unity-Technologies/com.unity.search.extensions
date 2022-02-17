using NUnit.Framework;
using UnityEditor.Search;

public class LightExplorerTests
{
    [Test]
    public void CheckProvider()
    {
        Assert.IsNotNull(SearchService.GetProvider("lightexplorer"));
    }
}
