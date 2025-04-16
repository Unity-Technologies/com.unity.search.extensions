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
        var scriptInPackage = "Packages/com.unity.search.extensions.examples/Editor/CustomIndexers/ShaderIndexing.cs";
        yield return new CustomIndexationTestCase("t:script", scriptInPackage, true);
        yield return new CustomIndexationTestCase("t:script", new string[] { scriptInPackage }, new string[] { scriptInPackage });

        var shaderInPackage = "Packages/com.unity.search.extensions.examples/Materials/NewSurfaceShader.shader";
        yield return new CustomIndexationTestCase("t:shader shader_tag.rendertype=opaque", shaderInPackage, true);

        // var textureInPackage = "Packages/com.unity.search.extensions.examples/Textures/Alcove.png";
        // yield return new CustomIndexationTestCase("t:texture2d texture2d.testismobilefriendly=false", textureInPackage, true);
        
        // var resourceProducer = "Packages/com.unity.search.extensions.examples/Prefabs/StoneProducer.prefab";
        // yield return new CustomIndexationTestCase("resourcetyperef=stone", resourceProducer, true);

        var resourceReserve = "Packages/com.unity.search.extensions.examples/ScriptableObjects/WoodReserve.asset";
        yield return new CustomIndexationTestCase("resourcetyperef=wood", resourceReserve, true);
    }

    [UnityTest]
    public IEnumerator ValidateCustomIndexation([ValueSource(nameof(GetCustomIndexationTestCases))] CustomIndexationTestCase tc)
    {
        var root = "Assets";
        if (tc.files[0].StartsWith("Packages"))
        {
            var packageNameIndex = tc.files[0].IndexOf("/", "Packages/".Length);
            root = tc.files[0].Substring(0, packageNameIndex);
        }

        var indexer = CustomIndexerUtilities.CreateIndexer(root, "asset", types: true, properties: true, dependencies: true, extended: false, tc.files);
        yield return CustomIndexerUtilities.RunIndexingAsync(indexer);
        Assert.IsTrue(indexer.IsReady());
        var results = CustomIndexerUtilities.Search(indexer, tc.query);
        Assert.AreEqual(tc.expectedFileCount, results.Count, $"Query {tc.query} yielded {results.Count} expected was {tc.expectedFileCount}");
        if (tc.expectedFiles != null)
            CollectionAssert.AreEquivalent(tc.expectedFiles, results);
    }
}
