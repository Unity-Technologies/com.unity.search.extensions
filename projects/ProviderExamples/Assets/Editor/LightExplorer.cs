using System;
using System.Collections.Generic;

namespace UnityEditor.Search
{
    static class LightExplorer
    {
        const string providerId = "lightexplorer";

        [SearchItemProvider]
        internal static SearchProvider RegisterLightExplorer()
        {
            return new SearchProvider(providerId, "Lights", SearchLights)
            {
                active = false, // Only activate this provider for contextual searches (ignore for global searches)
                fetchColumns = FetchColumns
            };
        }

        [MenuItem("Window/Search/Light Explorer")]
        internal static void ShowLightExplorer()
        {
            var context = SearchService.CreateContext(providerId, string.Empty);
            var viewFlags = UnityEngine.Search.SearchViewFlags.DisableSavedSearchQuery | UnityEngine.Search.SearchViewFlags.TableView;
            var viewState = new SearchViewState(context, viewFlags) { title = "Lights" };
            var qs = SearchService.ShowWindow(viewState) as QuickSearch;
            var tableView = qs.resultView as TableView;
            tableView.SetSearchTable(new SearchTable(Guid.NewGuid().ToString("N"), "LightExplorer", CreateColumns()));
        }

        static IEnumerable<SearchColumn> CreateColumns()
        {
            var flags = SearchColumnFlags.IgnoreSettings | SearchColumnFlags.CanSort;
            const string propertyProvider = "Experimental/SerializedProperty";
            yield return new SearchColumn("Enabled", "enabled", "GameObject/Enabled", null, flags) { width = 56f, options = SearchColumnFlags.TextAlignmentCenter };
            yield return new SearchColumn("Name", "name", "name", null, flags) { width = 150f };
            yield return new SearchColumn("Type", "#m_Type", propertyProvider, null, flags) { width = 100f };
            yield return new SearchColumn("Shape", "#m_Shape", propertyProvider, null, flags) { width = 70f };
            yield return new SearchColumn("Mode", "#m_Lightmapping", propertyProvider, null, flags) { width = 70f };
            yield return new SearchColumn("Color", "#m_Color", propertyProvider, null, flags) { width = 80f };
            yield return new SearchColumn("Intensity", "#m_Intensity", propertyProvider, null, flags) { width = 80f };
            yield return new SearchColumn("Indirect Multiplier", "#m_BounceIntensity", propertyProvider, null, flags) { width = 110f };
            yield return new SearchColumn("Shadows", "#m_Shadows.m_Type", propertyProvider, null, flags) { width = 110f };
        }

        static IEnumerable<SearchItem> SearchLights(SearchContext context, SearchProvider provider)
        {
            using (var sceneContext = SearchService.CreateContext("scene", BuildQuery(context)))
            using (var sceneRequest = SearchService.Request(sceneContext))
            {
                foreach (var r in sceneRequest)
                    yield return r;
            }
        }

        static string BuildQuery(in SearchContext userContext)
        {
            string query = "t=Light";
            if (!userContext.empty)
                query += $" ({userContext.searchQuery})";
            return query;
        }

        static IEnumerable<SearchColumn> FetchColumns(SearchContext context, IEnumerable<SearchItem> items)
        {
            return SearchService.GetProvider("scene").fetchColumns(context, items);
        }
    }
}
