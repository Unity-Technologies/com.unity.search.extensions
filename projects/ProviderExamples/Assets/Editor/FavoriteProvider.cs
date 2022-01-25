using System.Collections.Generic;

namespace UnityEditor.Search.Providers
{
    static class FavoriteSearchProvider
    {
        [MenuItem("Window/Search/Favorites")]
        internal static void ShowFavExplorer()
        {
            var context = SearchService.CreateContext(new SearchProvider("fav", "Favorites", FetchItems), string.Empty);
            var viewState = new SearchViewState(context) { title = "Favorites" };
            SearchService.ShowWindow(viewState);
        }

        static IEnumerable<SearchItem> FetchItems(SearchContext context, SearchProvider provider)
        {
            using (var subContext = SearchService.CreateContext("find", context.searchQuery.Length < 2 ? $"*.* {context.searchQuery}" : context.searchQuery, context.options))
            using (var results = SearchService.Request(subContext))
                foreach (var r in results)
                {
                    if (r == null || r.score >= 0)
                        yield return null;
                    else
                        yield return r;
                }
        }
    }
}