using UnityEditor;
using UnityEditor.Search;
using UnityEngine.Search;

static class SearchWindows
{
	[MenuItem("Window/Search/Views/Simple Search Bar 1")] public static void SearchViewFlags1() => CreateWindow(SearchViewFlags.None);
	[MenuItem("Window/Search/Views/Simple Search Bar 2")] public static void SearchViewFlags2() => CreateWindow(SearchViewFlags.EnableSearchQuery);
	[MenuItem("Window/Search/Views/Simple Search Bar 3")] public static void SearchViewFlags3() => CreateWindow(SearchViewFlags.DisableInspectorPreview);
	[MenuItem("Window/Search/Views/Simple Search Bar 4")] public static void SearchViewFlags4() => CreateWindow(SearchViewFlags.EnableSearchQuery | SearchViewFlags.DisableInspectorPreview);

	static void CreateWindow(SearchViewFlags flags)
	{
		var searchContext = SearchService.CreateContext(string.Empty);
		var viewArgs = new SearchViewState(searchContext, SearchViewFlags.CompactView | flags) { title = flags.ToString() };
		SearchService.ShowWindow(viewArgs);
	}
}
