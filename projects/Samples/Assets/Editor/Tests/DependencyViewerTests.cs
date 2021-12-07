using NUnit.Framework;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine.TestTools;

class DependencyViewerTests
{
    [OneTimeSetUp]
    public void BuildDatabase()
    {
        Dependency.Build();
    }

    [UnitySetUp]
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

    [UnityTest]
    public IEnumerator OpenDependencyViewer()
    {
        EditorApplication.ExecuteMenuItem("Window/Search/Dependency Viewer");
        yield return null;

        var viewer = EditorWindow.GetWindow<DependencyViewer>();
        Assert.IsNotNull(viewer, "Failed to open dependency viewer");

        Selection.activeObject = AssetDatabase.LoadMainAssetAtPath("Assets/Editor/Providers/EasySearchProviderExample.cs");
        yield return null;

        while (!viewer.IsReady())
            yield return null;

        viewer.Close();
    }
}