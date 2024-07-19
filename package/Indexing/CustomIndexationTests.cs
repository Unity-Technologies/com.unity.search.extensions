using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.TestTools;


public class CustomIndexationTests
{
    public class CustomIndexationTestCase
    {
        public CustomIndexationTestCase(string query, string file, bool shouldBeFound)
        {
            this.query = query;
            this.files = new[] { file };
            this.expectedFileCount = shouldBeFound ? 1 : 0;
        }

        public CustomIndexationTestCase(string query, string[] files, int expectedCount)
        {
            this.query = query;
            this.files = files;
            this.expectedFileCount = expectedCount;
        }

        public CustomIndexationTestCase(string query, string[] files, string[] expectedFiles)
        {
            this.query = query;
            this.files = files;
            this.expectedFiles = expectedFiles;
            expectedFileCount = expectedFiles.Length;
        }

        public override string ToString()
        {
            var expectedFiles = this.expectedFiles != null ? $"[{string.Join(',', this.expectedFiles)}]" : "";
            return $"{query} [{string.Join(',', files)}] => {expectedFileCount} {expectedFiles}";
        }

        public string[] files;
        public string query;
        public string[] expectedFiles;
        public int expectedFileCount;
    }

    public static IEnumerable<CustomIndexationTestCase> GetCustomIndexationTestCases()
    {
        /*
        var matFile = "Assets/Materials/done_fx_bolt_cyan_mat.mat";
        yield return new CustomIndexationTestCase("t:material", matFile, true);
        yield return new CustomIndexationTestCase("t:material", new string[] { matFile }, new string[] { matFile });
        */

        var scriptInPackage = "Packages/com.unity.search.extensions/Indexing/ShaderIndexing.cs";
        yield return new CustomIndexationTestCase("t:script", scriptInPackage, true);
        yield return new CustomIndexationTestCase("t:script", new string[] { scriptInPackage }, new string[] { scriptInPackage });

        var shaderInPackage = "Assets/Materials/SurfaceShader.shader";
        yield return new CustomIndexationTestCase("t:shader sh_rendertype=opaque", shaderInPackage, true);

        // Add your custom indexer tests here:
    }

    // [UnityTest]
    public IEnumerator ValidateCustomIndexation([ValueSource(nameof(GetCustomIndexationTestCases))] CustomIndexationTestCase tc)
    {
        var root = "Assets";
        if (tc.files[0].StartsWith("Packages"))
        {
            var packageNameIndex = tc.files[0].IndexOf("/", "Packages/".Length);
            root = tc.files[0].Substring(0, packageNameIndex);
        }

        var indexer = CustomIndexerUtilities.CreateIndexer(root, "asset", types: true, properties: false, dependencies: false, extended: false, tc.files);
        yield return CustomIndexerUtilities.RunIndexingAsync(indexer);
        Assert.IsTrue(indexer.IsReady());
        var results = CustomIndexerUtilities.Search(indexer, tc.query);
        Assert.AreEqual(tc.expectedFileCount, results.Count, $"Query {tc.query} yielded {results.Count} expected was {tc.expectedFileCount}");
        if (tc.expectedFiles != null)
            CollectionAssert.AreEquivalent(tc.expectedFiles, results);
    }
}
