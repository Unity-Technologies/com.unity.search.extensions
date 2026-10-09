#if !UNITY_7000_0_OR_NEWER
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
#endif
