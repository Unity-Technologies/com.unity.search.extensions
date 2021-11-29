using NUnit.Framework;
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
        new TestCase("e7969613e431dd449966876222fc5d21", "Assets/Materials/Red.mat", isLabel: true),
        new TestCase("is:file -is:package", "Assets/Editor/com.unity.search.extensions.tests.asmdef", isLabel: true),
        new TestCase("is:folder -is:package", "Assets/Editor", isLabel: true),
        new TestCase("is:broken", "ProjectSettings/ProjectSettings.asset", isLabel: true),
        new TestCase("is:missing in=1", "388060bf34f9a6a40bafbac77240e259", isLabel: true),
        new TestCase("from=Assets/Runtime/ManyRefs.asset", "Packages/com.unity.search.extensions.shared.assets/Prefabs/Simple.prefab", isLabel: true),
        new TestCase("in>=2", "ProjectSettings/ProjectSettings.asset", isLabel: true),
        new TestCase("from=[Assets/Runtime/ManyRefs.asset]", "10dc1e46f5f3dda43938758225fafe87"),
        new TestCase("ref=Assets/Materials/Red.mat", "89c8b58050d468e449bbfdcb7ffc7f68"),
        new TestCase("out>5", "85afa418919e4626a5688f4394b60dc4"),
        new TestCase("dep:in=0 is:file", "Packages/com.unity.test-framework/UnityEngine.TestRunner/Utils/QuaternionEqualityComparer.cs", isLabel: true),  // Unused assets
    };

    [OneTimeSetUp]
    public void BuildDatabase()
    {
        Dependency.Build();
        provider = SearchService.GetProvider(Dependency.providerId);
    }

    [UnitySetUp]
    public IEnumerator IsDatabaseReady()
    {
        while (!Dependency.IsReady())
            yield return null;
    }

    [UnityTest]
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