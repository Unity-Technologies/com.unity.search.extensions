using System;
#if !USE_SEARCH_MODULE
using System.Collections.Concurrent;
#endif
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditorInternal;
using UnityEngine;

#pragma warning disable UNT0007 // Null coalescing on Unity objects

namespace UnityEditor.Search
{
    static class Dependency
    {
        public const string providerId = "dep";
        public const string dependencyIndexLibraryPath = "Library/dependencies_v1.index";
        
        static DependencyIndexer index;

        #if !USE_SEARCH_MODULE
        readonly static ConcurrentDictionary<string, int> usedByCounts = new ConcurrentDictionary<string, int>();
        #endif

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
                startDrag = StartDrag
            };
        }

        [SearchActionsProvider]
        internal static IEnumerable<SearchAction> ActionHandlers()
        {
            yield return SelectAsset();
            yield return Goto("Uses", "Show Used By Dependencies", "from");
            yield return Goto("Used By", "Show Uses References", "ref");
            yield return Goto("Missing", "Show broken links", "is:missing from");
            yield return LogRefs();
        }

        [SearchSelector("refCount", provider: providerId)]
        internal static object SelectReferenceCount(SearchItem item)
        {
            var count = GetReferenceCount(item.id);
            if (count < 0)
                return null;
            return count;
        }

        [MenuItem("Window/Search/Rebuild dependency index", priority = 5677)]
        public static void Build()
        {
            index = new DependencyIndexer();
            index.Setup();
            Task.Run(() => RunThreadIndexing(index));
        }

        [MenuItem("Window/Search/Dependencies", priority = 5678)]
        internal static void OpenDependencySearch()
        {
            SearchService.ShowContextual(providerId);
        }

        [MenuItem("Assets/Depends/Copy GUID")]
        internal static void CopyGUID()
        {
            var obj = Selection.activeObject;
            if (!obj)
                return;
            EditorGUIUtility.systemCopyBuffer = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj));
        }

        [MenuItem("Assets/Depends/Find Uses")]
        internal static void FindUsings()
        {
            var obj = Selection.activeObject;
            if (!obj)
                return;
            var path = AssetDatabase.GetAssetPath(obj);
            var searchContext = SearchService.CreateContext(providerId, $"from=\"{path}\"");
            SearchService.ShowWindow(searchContext, "Dependencies (Uses)", saveFilters: false);
        }

        [MenuItem("Assets/Depends/Find Used By (References)")]
        internal static void FindUsages()
        {
            var obj = Selection.activeObject;
            if (!obj)
                return;
            var path = AssetDatabase.GetAssetPath(obj);
            var searchContext = SearchService.CreateContext(new[] { "dep", "scene", "asset", "adb" }, $"ref=\"{path}\"");
            SearchService.ShowWindow(searchContext, "References", saveFilters: false);
        }

        public static int GetReferenceCount(string id)
        {
            int usedByCount = -1;
            #if USE_SEARCH_MODULE
            var recordKey = PropertyDatabase.CreateRecordKey(id, "referenceCount");
            using (var view = SearchMonitor.GetView())
            {
                if (view.TryLoadProperty(recordKey, out object data))
                    return (int)data;
            }
            #else
            if (usedByCounts.TryGetValue(id, out usedByCount))
                return usedByCount;
            #endif

            var path = AssetDatabase.GUIDToAssetPath(id);
            if (path == null || Directory.Exists(path))
                return -1;

            var searchContext = SearchService.CreateContext(providerId, $"ref=\"{path}\"");
            SearchService.Request(searchContext, (context, items) =>
            {
                usedByCount = items.Count;
                #if USE_SEARCH_MODULE
                using (var view = SearchMonitor.GetView())
                {
                    view.Invalidate(recordKey);
                    view.StoreProperty(recordKey, usedByCount);
                }
                #else
                usedByCounts[id] = usedByCount;
                #endif
                context.Dispose();
            });
            return usedByCount;
        }

        static void Load(string indexPath)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var indexBytes = File.ReadAllBytes(indexPath);
            index = new DependencyIndexer();
            index.LoadBytes(indexBytes, (success) => ResolveLoadIndex(success, indexPath, indexBytes, sw));
        }

        static void RunThreadIndexing(DependencyIndexer index)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var metaFiles = Directory.GetFiles("Assets", "*.meta", SearchOption.AllDirectories);
            var progressId = Progress.Start($"Building dependency index ({metaFiles.Length} assets)");

            index.Start();
            index.Build(progressId, metaFiles);

            Progress.Report(progressId, -1f, $"Saving dependency index at {dependencyIndexLibraryPath}");
            index.Finish((bytes) =>
            {
                File.WriteAllBytes(dependencyIndexLibraryPath, bytes);
                Progress.Finish(progressId, Progress.Status.Succeeded);

                Debug.Log($"Dependency indexing took {sw.Elapsed.TotalMilliseconds,3:0.##} ms " +
                    $"and was saved at {dependencyIndexLibraryPath} ({EditorUtility.FormatBytes(bytes.Length)} bytes)");
            }, removedDocuments: null);
        }

        static UnityEngine.Object ToObject(SearchItem item, Type type)
        {
            if (item.options.HasAny(SearchItemOptions.FullDescription))
            {
                var depInfo = ScriptableObject.CreateInstance<DependencyInfo>();
                depInfo.Load(item);
                return depInfo;
            }
            else
                return GetObject(item);
        }

        static Texture2D FetchThumbnail(SearchItem item, SearchContext context)
        {
            if (index.ResolveAssetPath(item.id, out var path))
                return AssetDatabase.GetCachedIcon(path) as Texture2D ?? InternalEditorUtility.GetIconForFile(path);
            return null;
        }

        static void LoadGlobalIndex()
        {
            if (index == null)
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

        static SearchAction LogRefs()
        {
            return new SearchAction(providerId, "log", null, "Log references and usages", (SearchItem[] items) =>
            {
                foreach (var item in items)
                {
                    var sb = new StringBuilder();
                    if (index.ResolveAssetPath(item.id, out var assetPath))
                        sb.AppendLine($"Dependency info: {LogAssetHref(assetPath)}");
                    using (var context = SearchService.CreateContext(new string[] { providerId }, $"from:{item.id}"))
                    {
                        sb.AppendLine("outgoing:");
                        foreach (var r in SearchService.GetItems(context, SearchFlags.Synchronous))
                            LogRefItem(sb, r);
                    }

                    using (var context = SearchService.CreateContext(new string[] { providerId }, $"to:{item.id}"))
                    {
                        sb.AppendLine("incoming:");
                        foreach (var r in SearchService.GetItems(context, SearchFlags.Synchronous))
                            LogRefItem(sb, r);
                    }

                    Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, sb.ToString());
                }
            })
            {
                closeWindowAfterExecution = false
            };
        }

        static string LogAssetHref(string assetPath)
        {
            return $"<a href=\"{assetPath}\" line=\"0\">{assetPath}</a>";
        }

        static void LogRefItem(StringBuilder sb, SearchItem item)
        {
            if (index.ResolveAssetPath(item.id, out var assetPath))
                sb.AppendLine($"\t{LogAssetHref(assetPath)} ({item.id})");
            else
                sb.AppendLine($"\t<color=#EE9898>BROKEN</color> ({item.id})");
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
        }

        static void ResolveLoadIndex(bool success, string indexPath, byte[] indexBytes, System.Diagnostics.Stopwatch sw)
        {
            if (!success)
                Debug.LogError($"Failed to load dependency index at {indexPath}");
            else
                Debug.Log($"Loading dependency index took {sw.Elapsed.TotalMilliseconds,3:0.##} ms ({EditorUtility.FormatBytes(indexBytes.Length)} bytes)");
            SearchMonitor.contentRefreshed -= OnContentChanged;
            SearchMonitor.contentRefreshed += OnContentChanged;
        }

        static void OnContentChanged(string[] updated, string[] removed, string[] moved)
        {
            index?.Update(updated, removed, moved);
        }
    }
}
