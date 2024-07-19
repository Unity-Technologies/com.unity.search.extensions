using NUnit.Framework;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine.TestTools;

class DependencyViewerTests
{
    // [OneTimeSetUp]
    public void BuildDatabase()
    {
        Dependency.Build();
    }

    // [UnitySetUp]
    public IEnumerator IsDatabaseReady()
    {
        while (!Dependency.IsReady())
            yield return null;

        using (var qs = SearchService.ShowWindow())
        {
            while (qs.context.searchInProgress)
                yield return null;
        }
    }

    // [UnityTest]
    public IEnumerator OpenDependencyViewer()
    {
        EditorApplication.ExecuteMenuItem("Window/Search/Dependency Viewer");
        yield return null;

        var viewer = EditorWindow.GetWindow<DependencyViewer>();
        Assert.IsNotNull(viewer, "Failed to open dependency viewer");

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Editor/com.unity.search.extensions.tests.asmdef");
        yield return null;

        while (!viewer.IsReady())
            yield return null;

        #if UNITY_2021_2_OR_NEWER
        CollectionAssert.Contains(viewer.GetUses(), "388060bf34f9a6a40bafbac77240e259");
        #endif
        CollectionAssert.Contains(viewer.GetUsedBy(), "953ccea3a4c9ed44381fc3c5e3904df2");

        viewer.Close();
    }
}