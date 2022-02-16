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
    public static class Dependency
    {
        public const string providerId = "dep";
        public const string ignoreDependencyLabel = "ignore";
        public const string dependencyIndexLibraryPath = "Library/dependencies_v2.index";
        public const string refDepthField = "refDepth";
        public const string refDepthColumnFormat = "DependencyDepth";

        readonly static Regex fromRx = new Regex(@"from=(?:""?([^""]+)""?)");

        static DependencyIndexer index;
        static bool needUpdate { get; set; }
        static Task updateTask;
        readonly static ConcurrentDictionary<string, int> usedByCounts = new ConcurrentDictionary<string, int>();

        public static event Action indexingFinished;

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

        [SearchActionsProvider]
        internal static IEnumerable<SearchAction> ActionHandlers()
        {
            yield return SelectAsset();
            yield return Goto("Uses", "Show Used By Dependencies", "from");
            yield return Goto("Used By", "Show Uses References", "ref");
            yield return Goto("Missing", "Show broken links", "is:missing from");
            yield return CopyLabel();
        }

        [SearchSelector("refCount", provider: providerId)]
        internal static object SelectReferenceCount(SearchItem item)
        {
            var count = GetReferenceCount(item.id);
            if (count < 0)
                return null;
            return count;
        }

        [SearchSelector("type", provider: providerId)]
        internal static object SelectDependencyType(SearchItem item)
        {
            if (DependencyIndexer.builtinGuids.TryGetValue(item.id, out var name))
                return name;
            return AssetDatabase.GetMainAssetTypeAtPath(AssetDatabase.GUIDToAssetPath(item.id))?.Name ?? "None";
        }

        [SearchSelector("path", provider: providerId)]
        internal static object AssetPath(SearchItem item)
        {
            return AssetDatabase.GUIDToAssetPath(item.id);
        }

        [MenuItem("Window/Search/Rebuild dependency index", priority = 5677)]
        public static void Build()
        {
            usedByCounts.Clear();
            index = new DependencyIndexer();
            index.Setup();
            Task.Run(() => RunThreadIndexing(index));
        }

        [MenuItem("Window/Search/Dependencies", priority = 5678)]
        internal static void OpenDependencySearch()
        {
            SearchService.ShowContextual(providerId);
        }

        internal static bool HasIndex()
        {
            return index != null && index.documentCount > 0;
        }

        [MenuItem("Assets/Dependencies/Copy GUID", priority = 10001)]
        internal static void CopyGUID()
        {
            var obj = Selection.activeObject;
            if (!obj)
                return;
            EditorGUIUtility.systemCopyBuffer = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj));
        }

        [MenuItem("Assets/Dependencies/Find Uses", priority = 10001)]
        internal static void FindUsings()
        {
            var obj = Selection.activeObject;
            if (!obj)
                return;
            var path = AssetDatabase.GetAssetPath(obj);
            var searchContext = SearchService.CreateContext(providerId, $"from=\"{path}\"");
            SearchService.ShowWindow(searchContext, "Dependencies (Uses)", saveFilters: false);
        }

        [MenuItem("Assets/Dependencies/Find Uses (Recursive)", priority = 10001)]
        internal static void FindUsingsRecursive()
        {
            var obj = Selection.activeObject;
            if (!obj)
                return;
            var path = AssetDatabase.GetAssetPath(obj);
            var query = CreateUsingQuery(new[] { path }, 10 );
            var searchContext = SearchService.CreateContext(new[] { "dep", "scene", "asset", "adb" }, query);
            SearchService.ShowWindow(searchContext, "Dependencies (Uses)", saveFilters: false);
        }

        internal static IEnumerable<string> EscapePaths(IEnumerable<string> initialPaths)
        {
            return initialPaths.Select(p => p.StartsWith("\"") && p.EndsWith("\"") ? p : $"\"{p}\"");
        }

        internal static string CreateUsingQuery(IEnumerable<string> initialPaths, int depthLevel = 1)
        {
            // Depth level is 1 based: 1 -> direct dependencies, 2 -> 2nd level of dependencies.
            if (depthLevel <= 0)
                throw new Exception("Cannot compute using query with 0 depth.");

            var escapedPaths = EscapePaths(initialPaths);
            var initialSetQuery = escapedPaths.Count() == 1 ? $"from={escapedPaths.First()}" : $"from=[{string.Join(",", escapedPaths)}]";
            if (depthLevel == 1)
                return initialSetQuery;
            #if UNITY_2022_2_OR_NEWER
            return $"aggregate{{{initialSetQuery}, from=\"@path\", {depthLevel - 1}, {refDepthField}, keep, sort}}";
            #else
            throw new NotSupportedException("Dependency depth level is not supported in this version");
            #endif
        }

        [MenuItem("Assets/Dependencies/Find Used By (References)", priority = 10100)]
        internal static void FindUsages()
        {
            var obj = Selection.activeObject;
            if (!obj)
                return;
            var path = AssetDatabase.GetAssetPath(obj);
            var searchContext = SearchService.CreateContext(new[] { "dep", "scene", "asset", "adb" }, $"ref=\"{path}\"");
            SearchService.ShowWindow(searchContext, "References", saveFilters: false);
        }

        [MenuItem("Assets/Dependencies/Add to ignored", true, priority = 10200)]
        internal static bool CanAddToIgnoredList()
        {
            var obj = Selection.activeObject;
            if (!obj)
                return false;
            return !AssetDatabase.GetLabels(obj).Select(l => l.ToLowerInvariant()).Contains(ignoreDependencyLabel);
        }

        [MenuItem("Assets/Dependencies/Add to ignored", priority = 10200)]
        internal static void AddToIgnoredList()
        {
            var obj = Selection.activeObject;
            if (!obj)
                return;

            var labels = AssetDatabase.GetLabels(obj);
            AssetDatabase.SetLabels(obj, labels.Concat(new[] { ignoreDependencyLabel }).ToArray());
        }

        [MenuItem("Assets/Dependencies/Remove from ignored", true, priority = 10200)]
        internal static bool CanRemoveToIgnoredList()
        {
            var obj = Selection.activeObject;
            if (!obj)
                return false;
            return AssetDatabase.GetLabels(obj).Select(l => l.ToLowerInvariant()).Contains(Dependency.ignoreDependencyLabel);
        }

        [MenuItem("Assets/Dependencies/Remove from ignored", priority = 10200)]
        internal static void RemoveToIgnoredList()
        {
            var obj = Selection.activeObject;
            if (!obj)
                return;

            var labels = AssetDatabase.GetLabels(obj).Where(l => l.ToLowerInvariant() != Dependency.ignoreDependencyLabel).ToArray();
            AssetDatabase.SetLabels(obj, labels);
        }

        public static bool IsReady()
        {
            return index != null && index.IsReady() && (updateTask?.IsCompleted ?? true);
        }

        public static int GetReferenceCount(string id)
        {
            int usedByCount = -1;
            if (usedByCounts.TryGetValue(id, out usedByCount))
                return usedByCount;

            var path = AssetDatabase.GUIDToAssetPath(id);
            if (path == null || Directory.Exists(path))
                return -1;

            var searchContext = SearchService.CreateContext(providerId, $"ref=\"{path}\"");
            SearchService.Request(searchContext, (context, items) =>
            {
                usedByCount = items.Count;
                usedByCounts[id] = usedByCount;
                context.Dispose();
            });
            return usedByCount;
        }

        internal static IEnumerable<string> EnumerateIdFromObjects(IEnumerable<UnityEngine.Object> objects)
        {
            foreach (var obj in objects)
                yield return GlobalObjectId.GetGlobalObjectIdSlow(obj.GetInstanceID()).ToString();
        }

        internal static IEnumerable<string> EnumeratePaths(IEnumerable<string> globalIds)
        {
            return EnumerateIdInfos(globalIds).Select(info => info.path);
        }

        internal static IEnumerable<IdInfo> EnumerateIdInfos(IEnumerable<string> globalIds)
        {
            if (globalIds == null || globalIds.Any() == false)
                yield break;

            foreach (var sgid in globalIds)
            {
                if (!GlobalObjectId.TryParse(sgid, out var gid))
                    continue;

                var info = new IdInfo();
                info.globalId = gid.ToString();
                info.instanceID = GlobalObjectId.GlobalObjectIdentifierToInstanceIDSlow(gid);
                info.path = AssetDatabase.GetAssetPath(info.instanceID);
                if (!string.IsNullOrEmpty(info.path))
                {
                    info.isAssetId = true;
                    yield return info;
                }
                else if (EditorUtility.InstanceIDToObject(info.instanceID) is UnityEngine.Object obj)
                {
                    info.path = SearchUtils.GetObjectPath(obj).Substring(1);
                    yield return info;
                }
            }
        }

        static void Load(string indexPath)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var indexBytes = File.ReadAllBytes(indexPath);
            index = new DependencyIndexer();
            index.LoadBytes(indexBytes, (success) => ResolveLoadIndex(success, indexPath, indexBytes, sw));
        }

        static IEnumerable<string> EnumerateFiles()
        {
            foreach (var f in Directory.GetFiles("Assets", "*.meta", SearchOption.AllDirectories))
                yield return f;

            foreach (var f in Directory.GetFiles("Packages", "*.meta", SearchOption.AllDirectories))
                yield return f;

            foreach (var f in Directory.GetFiles("ProjectSettings", "*.*", SearchOption.TopDirectoryOnly))
                yield return $"{f.Replace("\\", "/")}.meta";
        }

        static void RunThreadIndexing(DependencyIndexer index)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var metaFiles = EnumerateFiles().ToArray();
            var progressId = Progress.Start($"Building dependency index ({metaFiles.Length} assets)");

            index.Start();

            try
            {
                index.Build(progressId, metaFiles);

                Progress.Report(progressId, -1f, $"Saving dependency index at {dependencyIndexLibraryPath}");
                index.Finish((bytes) =>
                {
                    File.WriteAllBytes(dependencyIndexLibraryPath, bytes);
                    Progress.Finish(progressId, Progress.Status.Succeeded);

                    Debug.Log($"Dependency indexing took {sw.Elapsed.TotalMilliseconds,3:0.##} ms " +
                        $"and was saved at {dependencyIndexLibraryPath} ({EditorUtility.FormatBytes(bytes.Length)} bytes)");

                    indexingFinished?.Invoke();
                }, removedDocuments: null);
            }
            catch (System.Exception ex)
            {
                index = null;
                Debug.LogException(ex);

                Progress.SetDescription(progressId, ex.Message);
                Progress.Finish(progressId, Progress.Status.Failed);
            }
        }

        static UnityEngine.Object ToObject(SearchItem item, Type type)
        {
            if ((item.options & SearchItemOptions.FullDescription) == SearchItemOptions.FullDescription)
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
            DependencyUtils.PingAsset(AssetDatabase.GUIDToAssetPath(item.id));
        }

        static void StartDrag(SearchItem item, SearchContext context)
        {
            if (context.selection?.Count > 1)
            {
                var selectedObjects = context.selection.Select(i => GetObject(i));
                var paths = context.selection.Select(i => GetAssetPath(i)).ToArray();
                DependencyUtils.StartDrag(selectedObjects.ToArray(), paths, item.GetLabel(context, true));
            }
            else
                DependencyUtils.StartDrag(new[] { GetObject(item) }, new[] { GetAssetPath(item) }, item.GetLabel(context, true));
        }

        static string GetAssetPath(in SearchItem item)
        {
            return AssetDatabase.GUIDToAssetPath(item.id);
        }

        static UnityEngine.Object GetObject(in SearchItem item)
        {
            return AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(item.id));
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
            needUpdate = !GetDiff().empty;
        }

        static void OnContentChanged(string[] updated, string[] removed, string[] moved)
        {
            needUpdate = true;
        }

        internal static bool HasUpdate()
        {
            return needUpdate;
        }

        internal static AssetIndexChangeSet GetDiff()
        {
            return SearchMonitor.GetDiff(index.timestamp, Enumerable.Empty<string>(), s => true);
        }

        internal static void Update(Action finished)
        {
            var updateProgressId = Progress.Start("Update dependencies", null, Progress.Options.Indefinite);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            updateTask = index?.Update(GetDiff().all, (err, bytes) =>
            {
                if (err != null)
                {
                    Debug.LogException(err);
                    Progress.SetDescription(updateProgressId, err.Message);
                    Progress.Finish(updateProgressId, Progress.Status.Failed);
                    return;
                }

                needUpdate = false;
                File.WriteAllBytes(dependencyIndexLibraryPath, bytes);

                Debug.Log($"Dependency incremental update took {sw.Elapsed.TotalMilliseconds,3:0.##} ms " +
                    $"and was saved at {dependencyIndexLibraryPath} ({EditorUtility.FormatBytes(bytes.Length)} bytes)");

                finished?.Invoke();
                Progress.Finish(updateProgressId);
            });
        }

        [SearchColumnProvider(refDepthColumnFormat)]
        public static void DependencyDepthColumnProvider(SearchColumn column)
        {
            column.getter = args =>
            {
                if (args.item.TryGetField(Dependency.refDepthField, out var field))
                {
                    return (int)field.value + 1;
                }
                return 1;
            };
        }
    }
}
