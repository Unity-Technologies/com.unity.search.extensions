using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.Search.Collections
{
    [Serializable]
    class SearchCollection
    {
        public SearchCollection()
        {
            color = new Color(0, 0, 0, 0);
            items = new HashSet<SearchItem>();
            objects = new List<UnityEngine.Object>();
        }

        public SearchCollection(ISearchQuery searchQuery)
            : this()
        {
            guid = searchQuery.guid;
            name = searchQuery.displayName;
            searchText = searchQuery.searchText;
            providerIds = searchQuery.GetProviderIds().ToArray();
            icon = searchQuery.thumbnail;
        }

        public string guid;
        public string name;
        public string searchText;
        public string[] providerIds;
        public Texture2D icon;
        public Color color;
        public List<UnityEngine.Object> objects;

        [NonSerialized] public HashSet<SearchItem> items;

        public ISearchQuery searchQuery
        {
            get
            {
                if (AssetDatabase.GUIDToAssetPath(guid) is string assetPath && !string.IsNullOrEmpty(assetPath))
                    return AssetDatabase.LoadAssetAtPath<SearchQueryAsset>(assetPath);
                return SearchQuery.searchQueries.FirstOrDefault(sq => sq.guid == guid);
            }
        }

        [Serializable]
        class SearchCollections
        {
            public const string key = "SearchCollections_V2";

            public List<SearchCollection> collections;

            public SearchCollections()
            {
                collections = new List<SearchCollection>();
            }

            public SearchCollections(IEnumerable<SearchCollection> collections)
            {
                this.collections = collections.ToList();
            }
        }

        public static List<SearchCollection> LoadCollections()
        {
            SearchCollections collections = new SearchCollections();
            var collectionsJSON = EditorPrefs.GetString(SearchCollections.key, string.Empty);
            if (string.IsNullOrEmpty(collectionsJSON))
                return collections.collections;
            EditorJsonUtility.FromJsonOverwrite(collectionsJSON, collections);
            return collections.collections;
        }

        public static void SaveCollections(IEnumerable<SearchCollection> elements)
        {
            SearchCollections collections = new SearchCollections(elements);
            EditorPrefs.SetString(SearchCollections.key, EditorJsonUtility.ToJson(collections));
        }

        public static void SelectCollection(Action<SearchCollection> selected)
        {
            var context = SearchService.CreateContext(CreateSearchQueryProvider(), string.Empty);
            var viewState = new SearchViewState(context, (item, canceled) => SelectCollection(item, canceled, selected))
            {
                flags = UnityEngine.Search.SearchViewFlags.DisableInspectorPreview | UnityEngine.Search.SearchViewFlags.DisableSavedSearchQuery,
                itemSize = 1,
                title = "Collections",
                position = new Rect(0, 0, 350, 500),
                excludeNoneItem = true
            };
            SearchService.ShowPicker(viewState);
        }

        private static void SelectCollection(SearchItem item, bool canceled, Action<SearchCollection> selected)
        {
            if (canceled)
                return;

            if (!(item.data is ISearchQuery sq))
                return;

            selected(new SearchCollection(sq));
        }

        private static SearchProvider CreateSearchQueryProvider()
        {
            return new SearchProvider("csq", "Queries", FetchQueries);
        }

        private static IEnumerable<SearchItem> FetchQueries(SearchContext context, SearchProvider provider)
        {
            var searchQuery = context.searchQuery;
            var queryEmpty = string.IsNullOrEmpty(searchQuery);
            foreach (ISearchQuery sq in SearchQuery.searchQueries.Cast<ISearchQuery>().Concat(SearchQueryAsset.savedQueries))
            {
                if (!queryEmpty && !SearchUtils.MatchSearchGroups(context, sq.displayName, ignoreCase: true))
                    continue;

                var details = sq.details;
                if (string.IsNullOrEmpty(details))
                    details = sq.searchText;
                yield return provider.CreateItem(context, sq.guid, (int)~sq.lastUsedTime, sq.displayName, details, sq.thumbnail, sq);
            }
        }
    }
}
