#if USE_SEARCH_EXTENSION_API
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.Search
{
    static class TableExamples
    {
        [MenuItem("Search/Table Example")]
        public static void OpenTableView()
        {
            var context = SearchService.CreateContext("scene", "path:turntable t=MeshFilter");
            var viewState = new SearchViewState(context, new SearchTable("myColumnSetup", CreateColumns()));
            SearchService.ShowWindow(viewState);
        }

        static IEnumerable<SearchColumn> CreateColumns()
        {
            var flags = SearchColumnFlags.IgnoreSettings; // Set to SearchColumnFlags.Default if you want the system to restore any user changes to column's width
            yield return new SearchColumn("Name", "name", "name", null, flags) { width = 250f };
            yield return new SearchColumn("Mesh", "#m_Mesh", "ObjectReference", null, flags) { width = 180f };
            yield return new SearchColumn("Vertices", "vertices", "default", null, flags) { width = 60f };
            yield return new SearchColumn("Delete", null, null, null, flags) { width = 60f, drawer = OnDelete };
        }

        private static object OnDelete(SearchColumnEventArgs args)
        {
            if (GUI.Button(args.rect, "Delete"))
                Debug.LogWarning($"TODO: Delete {args.item.GetLabel(args.context, true)}");
            return args.value;
        }
    }
}
#endif
