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
        new TestCase("is:folder -is:package", "Assets/Prefabs", isLabel: true),
        new TestCase("is:broken", "Assets/Runtime/ManyRefs.asset", isLabel: true),
        new TestCase("is:missing in=1", "Packages/com.unity.search.extensions", isLabel: true), // We do not map folders?
		new TestCase("from=Assets/Runtime/ManyRefs.asset", "Assets/Prefabs/Simple.prefab", isLabel: true),
		new TestCase("from=Assets/Runtime/ManyRefs.asset in=1", "Assets/Scripts/ManyRefs.cs", isLabel: true),
		new TestCase("from=[Assets/Runtime/ManyRefs.asset]", "10dc1e46f5f3dda43938758225fafe87"),
		new TestCase("ref=Assets/Materials/Red.mat", "345b0b890043f484095fc55e158702b4"),
        new TestCase("out>5", "85afa418919e4626a5688f4394b60dc4"),

		new TestCase("dep:in=0 is:file -is:package", "Assets/Editor/DependencyProviderTests.cs", isLabel: true),  // Unused assets

		new TestCase("first{25,sort{select{p:a:assets, @path, count{dep:ref=\"@path\"}}, @value, desc}}", null, new string[]
		{
			"Assets/Prefabs/Simple.prefab",
			"Assets/Materials/Red.mat",
			"Assets/Runtime/ManyRefs.asset"
		})
	};

	[UnitySetUp]
	public IEnumerator BuildDatabase()
	{
		Dependency.Build();
		while (!Dependency.IsReady())
			yield return null;

		provider = SearchService.GetProvider(Dependency.providerId);
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
				CollectionAssert.IsSubsetOf(testCase.expectedIds, results.Select(r => r.id));

            if (testCase.expectedLabels != null)
                CollectionAssert.IsSubsetOf(testCase.expectedIds, results.Select(r => r.id));
        }
    }
}