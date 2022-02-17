using NUnit.Framework;
using UnityEditor.Search;

public class CheckProviders
{
    // A Test behaves as an ordinary method
    [Test]
    public void CheckDependencyProvider()
    {
        Assert.IsNotNull(SearchService.GetProvider("dep"));
    }
}
