#if UNITY_2021_2_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.Search.Collections
{
    [Serializable]
    class SearchCollection
    {
        [SerializeField] public string guid;
        [SerializeField] private string m_Name;
        [SerializeField] public string searchText;
        [SerializeField] public string[] providerIds;
        [SerializeField] public Color color;
        [SerializeField] private Texture2D m_Icon;
        [SerializeField] private List<string> m_gids;

        [NonSerialized] public HashSet<SearchItem> items;
        [NonSerialized] private HashSet<UnityEngine.Object> m_Objects;
        [NonSerialized] private ISearchQuery m_SearchQuery;

        public SearchCollection()
        {
            guid = null;
            m_Name = null;
            searchText = null;
            providerIds = null;
            color = new Color(0, 0, 0, 0);
            m_Icon = null;
            m_gids = new List<string>();
            items = new HashSet<SearchItem>();
            m_Objects = null;
            m_SearchQuery = null;
        }

        public SearchCollection(ISearchQuery searchQuery)
            : this()
        {
            guid = searchQuery.guid;
            m_Name = searchQuery.displayName;
            searchText = searchQuery.searchText;
            providerIds = searchQuery.GetProviderIds().ToArray();
            m_Icon = searchQuery.thumbnail;
        }

        public SearchCollection(string name)
            : this()
        {
            m_Name = name;
        }

        public string name
        {
            get
            {
                if (string.IsNullOrEmpty(m_Name) && searchQuery != null)
                    return searchQuery.displayName;
                return m_Name ?? string.Empty;
            }

            set
            {
                if (string.IsNullOrEmpty(value))
                    m_Name = null;
                else
                    m_Name = value;

                searchQuery.displayName = value;
            }
        }

        public ISearchQuery searchQuery
        {
            get
            {
                if (m_SearchQuery == null)
                {
                    if (AssetDatabase.GUIDToAssetPath(guid) is string assetPath && !string.IsNullOrEmpty(assetPath))
                    { 
                        m_SearchQuery = AssetDatabase.LoadMainAssetAtPath(assetPath) as ISearchQuery;
                    }
                    else
                    {
                        #if USE_SEARCH_EXTENSION_API
                        m_SearchQuery = SearchUtils.FindQuery(guid);
                        #else
                        m_SearchQuery = SearchQuery.searchQueries.FirstOrDefault(sq => sq.guid == guid);
                        #endif
                    }
                }
                return m_SearchQuery;
            }
        }

        public ISet<UnityEngine.Object> objects
        {
            get
            {
                if (m_Objects == null)
                    LoadObjects();
                return m_Objects;
            }
        }

        public Texture2D icon
        {
            get
            {
                return m_Icon;
            }

            set
            {
                m_Icon = value;
                #if USE_SEARCH_EXTENSION_API
                if (searchQuery != null) searchQuery.thumbnail = value;
                #else
                if (searchQuery is SearchQuery sq)
                    sq.thumbnail = value;
                #endif
            }
        }

        public void AddObject(UnityEngine.Object obj)
        {
            var gid = GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();
            m_gids.Add(gid);
            objects.Add(obj);
        }

        internal void AddObjects(UnityEngine.Object[] objs)
        {
            var gids = new GlobalObjectId[objs.Length];
            GlobalObjectId.GetGlobalObjectIdsSlow(objs, gids);
            m_gids.AddRange(gids.Select(g => g.ToString()));
            objects.UnionWith(objs);
        }

        public void RemoveObject(UnityEngine.Object obj)
        {
            if (m_Objects.Remove(obj))
            {
                var gid = GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();
                m_gids.Remove(gid.ToString());
            }
        }

        void LoadObjects()
        {
            var gids = m_gids.Select(id =>
            {
                if (GlobalObjectId.TryParse(id, out var gid))
                    return gid;
                return default;
            }).Where(g => g.identifierType != 0).ToArray();

            var objects = new UnityEngine.Object[gids.Length];
            GlobalObjectId.GlobalObjectIdentifiersToObjectsSlow(gids, objects);
            m_Objects = new HashSet<UnityEngine.Object>(objects);
        }

        public static List<SearchCollection> LoadCollections(string suffix = "")
        {
            SearchCollections collections = new SearchCollections();
            var collectionsJSON = EditorPrefs.GetString(SearchCollections.key + suffix, string.Empty);
            if (string.IsNullOrEmpty(collectionsJSON))
                return collections.collections;
            EditorJsonUtility.FromJsonOverwrite(collectionsJSON, collections);
            return collections.collections;
        }

        public static void SaveCollections(IEnumerable<SearchCollection> elements, string suffix = "")
        {
            SearchCollections collections = new SearchCollections(elements);
            EditorPrefs.SetString(SearchCollections.key + suffix, EditorJsonUtility.ToJson(collections));
        }

        public static void SelectCollection(Action<SearchCollection> selected)
        {
            var context = SearchService.CreateContext(CreateSearchQueryProvider(), string.Empty);
            var viewState = new SearchViewState(context, (item, canceled) => SelectCollection(item, canceled, selected))
            {
                flags = UnityEngine.Search.SearchViewFlags.DisableInspectorPreview 
                #if UNITY_2022_1_OR_NEWER
                | UnityEngine.Search.SearchViewFlags.DisableSavedSearchQuery,
                #if USE_SEARCH_EXTENSION_API
                excludeClearItem = true
                #else
                excludeNoneItem = true
                #endif
                #endif
                ,
                itemSize = 1,
                title = "Collections",
                position = new Rect(0, 0, 350, 500),
                
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
            #if USE_SEARCH_EXTENSION_API
            foreach (ISearchQuery sq in SearchUtils.EnumerateAllQueries())
            #else
            foreach (ISearchQuery sq in SearchQuery.searchQueries.Cast<ISearchQuery>().Concat(SearchQueryAsset.savedQueries))
            #endif
            {
                if (!queryEmpty && !SearchUtils.MatchSearchGroups(context, sq.displayName, ignoreCase: true))
                    continue;

                #if UNITY_2022_2_OR_NEWER
                int score  = (int)~sq.lastUsedTime;
                var details = sq.details;
                if (string.IsNullOrEmpty(details))
                    details = sq.searchText;
                #else
                var details = sq.searchText;
                int score = sq.displayName[0];
                #endif
                yield return provider.CreateItem(context, sq.guid, score, sq.displayName, details, sq.thumbnail, sq);
            }
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
}
#endif
