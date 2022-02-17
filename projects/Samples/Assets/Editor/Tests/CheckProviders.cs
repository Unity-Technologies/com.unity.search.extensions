using NUnit.Framework;
using UnityEditor.Search;

public class CheckProviders
{
    [Test]
    public void CheckDependencyProvider()
    {
        Assert.IsNotNull(SearchService.GetProvider("dep"));
    }
}
