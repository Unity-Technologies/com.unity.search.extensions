#if USE_SEARCH_TABLE
//#define DEBUG_DEPENDENCY_INDEXING
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditorInternal;
using UnityEngine;

#pragma warning disable UNT0007 // Null coalescing on Unity objects

namespace UnityEditor.Search
{
    struct SearchContextDescription
    {
        public string[] providers;
        public string searchQuery;
        public bool isValid => !string.IsNullOrEmpty(searchQuery);
        public SearchContext CreateContext()
        {
            if (!isValid)
                throw new Exception("Cannot create invalid context");

            if (providers == null)
                return SearchService.CreateContext(searchQuery);
            return SearchService.CreateContext(providers, searchQuery);
        }
    }

    static class Dependency
    {
        public const string ignoreDependencyLabel = "ignore";
        public const string providerId = "dep";
        public const string dependencyIndexLibraryPath = "Library/dependencies_v1.index";

        readonly static Regex fromRx = new Regex(@"from=(?:""?([^""]+)""?)");

        public static event Action indexingFinished;

        static DependencyIndexer index;

        readonly static ConcurrentDictionary<string, int> usedByCounts = new ConcurrentDictionary<string, int>();

        [SearchItemProvider]
        internal static SearchProvider CreateProvider()
        {
            return new SearchProvider(providerId, "Dependencies")
            {
                priority = 31,
                active = false,
                isExplicitProvider = false,
                showDetails = true,
                showDetailsOptions = ShowDetailsOptions.Inspector | ShowDetailsOptions.Actions,
                fetchItems = (context, items, provider) => FetchItems(context, provider),
                fetchLabel = FetchLabel,
                fetchDescription = FetchDescription,
                fetchThumbnail = FetchThumbnail,
                trackSelection = TrackSelection,
                toObject = ToObject,
                startDrag = StartDrag,
                #if USE_QUERY_BUILDER
                fetchPropositions = FetchPropositions
                #endif
            };
        }

        #if USE_QUERY_BUILDER
        [SearchTemplate(description = "Most Used Assets", providerId = "dep", viewFlags = UnityEngine.Search.SearchViewFlags.CompactView)]
        #endif
        internal static string GetMostUsedAssetsQuery()
        {
            return "first{25,sort{select{p:a:assets, @path, count{dep:ref=\"@path\"}}, @value, desc}}";
        }

        #if USE_QUERY_BUILDER
        private static IEnumerable<SearchProposition> FetchPropositions(SearchContext context, SearchPropositionOptions options)
        {
            string currentSelectedPath = null;
            if (Selection.assetGUIDs.Length > 0)
                currentSelectedPath = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]);
            yield return new SearchProposition(category: null, label: "Using (ref:)", 
                replacement: $"ref=<$object:{currentSelectedPath ?? "none"},UnityEngine.Object$>", icon: SearchUtils.GetTypeIcon(typeof(UnityEngine.Object)));
            yield return new SearchProposition(category: null, label: "Used By (from:)",
                replacement: $"from=<$object:{currentSelectedPath ?? "none"},UnityEngine.Object$>", icon: SearchUtils.GetTypeIcon(typeof(UnityEngine.Object)));
            yield return new SearchProposition(category: null, label: "Missing GUIDs",
                replacement: $"is:missing", icon: SearchUtils.GetTypeIcon(typeof(UnityEngine.Object)));
            yield return new SearchProposition(category: null, label: "Broken Assets",
                replacement: $"is:broken", icon: SearchUtils.GetTypeIcon(typeof(UnityEngine.Object)));
        }
        #endif

        [SearchActionsProv                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   if (index == null)
            {
                if (File.Exists(dependencyIndexLibraryPath))
                    Load(dependencyIndexLibraryPath);
                else
                    Build();
            }
        }

        static SearchAction SelectAsset()
        {
            return new SearchAction(providerId, "select", null, "Select", (SearchItem item) =>
            {
                if (index.ResolveAssetPath(item.id, out var path))
                    Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(path);
                else
                    item.context?.searchView?.SetSearchText($"dep: to:{item.id}");
            })
            {
                closeWindowAfterExecution = false
            };
        }

        static SearchAction CopyLabel()
        {
            return new SearchAction(providerId, "copy", null, "Copy", (SearchItem item) =>
            {
                var label = item.GetLabel(item.context, true).Trim();
                Debug.Log(label);
                EditorGUIUtility.systemCopyBuffer = label;
            })
            {
                closeWindowAfterExecution = false
            };
        }

        static SearchAction Goto(string action, string title, string filter)
        {
            return new SearchAction(providerId, action, null, title, item => Goto(item, filter)) { closeWindowAfterExecution = false };
        }

        static void Goto(SearchItem item, string filter)
        {
            if (item.context != null && item.context.searchView != null)
                item.context.searchView.SetSearchText($"dep: {filter}=\"{item.id}\"");
            else
            {
                var searchContext = SearchService.CreateContext(providerId, $"{filter}=\"{item.id}\"");
                SearchService.ShowWindow(searchContext, "Dependencies", saveFilters: false);
            }
        }

        static string FetchLabel(SearchItem item, SearchContext context)
        {
            var metaString = index.GetMetaInfo(item.id);
            var hasMetaString = !string.IsNullOrEmpty(metaString);
            if (index.ResolveAssetPath(item.id, out var path))
                return !hasMetaString ? path : $"<color=#EE9898>{path}</color>";

            return $"<color=#EE6666>{item.id}</color>";
        }

        static string GetDescription(SearchItem item)
        {
            var metaString = index.GetMetaInfo(item.id);
            if (!string.IsNullOrEmpty(metaString))
                return metaString;

            if (index.ResolveAssetPath(item.id, out _))
                return item.id;

            return "<invalid>";
        }

        static string FetchDescription(SearchItem item, SearchContext context)
        {
            var description = GetDescription(item);
            return $"{FetchLabel(item, context)} ({description})";
        }

        static void TrackSelection(SearchItem item, SearchContext context)
        {
            EditorGUIUtility.systemCopyBuffer = item.id;
            Utils.PingAsset(AssetDatabase.GUIDToAssetPath(item.id));
        }

        static void StartDrag(SearchItem item, SearchContext context)
        {
            if (context.selection?.Count > 1)
            {
                var selectedObjects = context.selection.Select(i => GetObject(i));
                var paths = context.selection.Select(i => GetAssetPath(i)).ToArray();
                Utils.StartDrag(selectedObjects.ToArray(), paths, item.GetLabel(context, true));
            }
            else
                Utils.StartDrag(new[] { GetObject(item) }, new[] { GetAssetPath(item) }, item.GetLabel(context, true));
        }

        static string GetAssetPath(in SearchItem item)
        {
            return AssetDatabase.GUIDToAssetPath(item.id);
        }

        static UnityEngine.Object GetObject(in SearchItem item)
        {
            #if USE_SEARCH_MODULE
            if (GUID.TryParse(item.id, out var guid))
                return AssetDatabase.LoadMainAssetAtGUID(guid);
            return null;
            #else
            return AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(item.id));
            #endif
        }

        static IEnumerable<SearchItem> FetchItems(SearchContext context, SearchProvider provider)
        {
            if (index == null)
                LoadGlobalIndex();
            while (index == null || !index.IsReady())
                yield return null;
            foreach (var e in index.Search(context.searchQuery.ToLowerInvariant(), context, provider))
            {
                var item = provider.CreateItem(context, e.id, e.score, null, null, null, e.index);
                item.options &= ~SearchItemOptions.Ellipsis;
                yield return item;
            }

            foreach (Match match in fromRx.Matches(context.searchQuery))
                foreach (var r in GetADBDependencies(match, context, provider))
                    yield return r;
        }

        private static IEnumerable<SearchItem> GetADBDependencies(Match match, SearchContext context, SearchProvider provider)
        {
            if (match.Groups.Count < 2)
                yield break;
            var assetPath = match.Groups[1].Value;
            foreach (var r in AssetDatabase.GetDependencies(assetPath, false))
            {
                var guid = AssetDatabase.AssetPathToGUID(r);
                if (!string.IsNullOrEmpty(guid))
                {
                    var item = provider.CreateItem(context, guid, 0, null, null, null, null);
                    item.options &= ~SearchItemOptions.Ellipsis;
                    yield return item;
                }
            }
        }

        static void ResolveLoadIndex(bool success, string indexPath, byte[] indexBytes, System.Diagnostics.Stopwatch sw)
        {
            if (!success)
                Debug.LogError($"Failed to load dependency index at {indexPath}");
            #if DEBUG_DEPENDENCY_INDEXING
            else
                Debug.Log($"Loading dependency index took {sw.Elapsed.TotalMilliseconds,3:0.##} ms ({EditorUtility.FormatBytes(indexBytes.Length)})");
            #endif
            SearchMonitor.contentRefreshed -= OnContentChanged;
            SearchMonitor.contentRefreshed += OnContentChanged;
        }

        static void OnContentChanged(string[] updated, string[] removed, string[] moved)
        {
            index?.Update(updated, removed, moved);
        }
    }
}
#endif
