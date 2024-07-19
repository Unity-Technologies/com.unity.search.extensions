using NUnit.Framework;
using NUnit.Framework.Internal;
using System.Collections;
using UnityEditor.Search;
using UnityEngine.TestTools;

class DependencyProviderTests
{
    public struct TestCase
    {
        public readonly string query;
        public readonly string[] expectedIds;
        public readonly string[] expectedLabels;

        public TestCase(string query, string[] expectedIds = null, string[] expectedLabels = null)
        {
            this.query = query;
            this.expectedIds = expectedIds ?? new string[0];
            this.expectedLabels = expectedLabels ?? new string[0];
        }

        public TestCase(string query, string expectedValue, bool isLabel = false)
            : this(query, isLabel ? null : new string[] { expectedValue }, isLabel ? new string[] { expectedValue } : null)
        {
        }

        public override string ToString()
        {
            return query;
        }
    }

    SearchProvider provider;
    static TestCase[] testCases = new TestCase[]
    {
        new TestCase("e7969613e431dd449966876222fc5d21", "Assets/Dependencies/Materials/Red.mat", isLabel: true),
        new TestCase("is:file -is:package", "Assets/Dependencies/Runtime/ManyRefs.asset", isLabel: true),
        new TestCase("is:folder -is:package", "Assets/Editor", isLabel: true),
        new TestCase("is:broken", "ProjectSettings/ProjectSettings.asset", isLabel: true),
        new TestCase("from=Assets/Dependencies/Runtime/ManyRefs.asset", "Assets/Dependencies/Prefabs/Simple.prefab", isLabel: true),
        new TestCase("in>=2", "Assets/Dependencies/Materials/Red.mat", isLabel: true),
        new TestCase("from=[Assets/Dependencies/Runtime/ManyRefs.asset]", "10dc1e46f5f3dda43938758225fafe87"),
        new TestCase("ref=Assets/Dependencies/Materials/Red.mat", "2c8433551883c9444a3e4442e53607fd"),
        new TestCase("out>5", "85afa418919e4626a5688f4394b60dc4"),
        new TestCase("dep:in=0 is:file", "Packages/com.unity.test-framework/UnityEngine.TestRunner/Utils/QuaternionEqualityComparer.cs", isLabel: true),  // Unused assets

        #if USE_SEARCH_MODULE
        new TestCase("is:missing in=1", "388060bf34f9a6a40bafbac77240e259", isLabel: true),
        #endif
    };

    // [OneTimeSetUp]
    public void BuildDatabase()
    {
        Dependency.Build();
        provider = SearchService.GetProvider(Dependency.providerId);
    }

    // [UnitySetUp]
    public IEnumerator IsDatabaseReady()
    {
        while (!Dependency.IsReady())
            yield return null;
    }

    // [UnityTest]
    public IEnumerator Query([ValueSource(nameof(testCases))] TestCase testCase)
    {
        using (var context = SearchService.CreateContext(provider, testCase.query))
        using (var results = SearchService.Request(context))
        {
            while (results.pending)
                yield return null;

            if (testCase.expectedIds != null)
                CollectionAssert.IsSupersetOf(results.Select(r => r.id), testCase.expectedIds);

            if (testCase.expectedLabels != null)
                CollectionAssert.IsSupersetOf(results.Select(r => r.GetLabel(context, stripHTML: true)), testCase.expectedLabels);
        }
    }
}