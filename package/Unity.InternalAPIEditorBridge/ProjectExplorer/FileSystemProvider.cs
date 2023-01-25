using System.Collections.Generic;
using System.Linq;
using UnityEditor.Search;
using UnityEditor;
using UnityEditor.Search.Providers;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

struct HierarchyPropertyCopy : IHierarchyProperty
{
    public void Reset()
    {
        throw new NotImplementedException();
    }

    public bool IsExpanded(int[] expanded)
    {
        throw new NotImplementedException();
    }

    public bool Next(int[] expanded)
    {
        throw new NotImplementedException();
    }

    public bool NextWithDepthCheck(int[] expanded, int minDepth)
    {
        throw new NotImplementedException();
    }

    public bool Previous(int[] expanded)
    {
        throw new NotImplementedException();
    }

    public bool Parent()
    {
        throw new NotImplementedException();
    }

    public bool Find(int instanceID, int[] expanded)
    {
        throw new NotImplementedException();
    }

    public int[] FindAllAncestors(int[] instanceIDs)
    {
        throw new NotImplementedException();
    }

    public bool Skip(int count, int[] expanded)
    {
        throw new NotImplementedException();
    }

    public int CountRemaining(int[] expanded)
    {
        throw new NotImplementedException();
    }

    public int GetInstanceIDIfImported()
    {
        throw new NotImplementedException();
    }

    public int instanceID { get; set; }
    public Object pptrValue { get; set; }
    public string name { get; set; }
    public bool hasChildren { get; set; }
    public int depth { get; set; }
    public int row { get; set; }
    public int colorCode { get; set; }
    public string guid { get; set; }
    public Texture2D icon { get; set; }
    public bool isValid { get; set; }
    public bool isMainRepresentation { get; set; }
    public bool hasFullPreviewImage { get; set; }
    public IconDrawStyle iconDrawStyle { get; set; }
    public bool isFolder { get; set; }
    public GUID[] dynamicDependencies { get; set; }
    public int[] ancestors { get; set; }
}

static class FileSystemProvider
{
    public const string type = "fs";

    static QueryEngine s_QueryEngine;
    static IQueryEngineFilter s_FolderFilter;
    static Texture2D noBadge;

    static FileSystemProvider()
    {
        noBadge = new Texture2D(1, 1);
        noBadge.SetPixel(0, 0, new Color(0, 0, 0, 0));
    }

    static SearchFilter.SearchArea GetSearchArea(in SearchFlags searchFlags)
    {
        if (searchFlags.HasAny(SearchFlags.Packages))
            return SearchFilter.SearchArea.AllAssets;
        return SearchFilter.SearchArea.InAssetsOnly;
    }

    static HierarchyPropertyCopy CopyPropertyData(HierarchyProperty property)
    {
        var copy = new HierarchyPropertyCopy();
        copy.instanceID = property.instanceID;
        copy.pptrValue = property.pptrValue;
        copy.name = property.name;
        copy.hasChildren = property.hasChildren;
        copy.depth = property.depth;
        copy.row = property.row;
        copy.colorCode = property.colorCode;
        copy.guid = property.guid;
        copy.icon = property.icon;
        copy.isValid = property.isValid;
        copy.isMainRepresentation = property.isMainRepresentation;
        copy.hasFullPreviewImage = property.hasFullPreviewImage;
        copy.iconDrawStyle = property.iconDrawStyle;
        copy.isFolder = property.isFolder;
        copy.dynamicDependencies = property.dynamicDependencies.ToArray();
        copy.ancestors = property.ancestors.ToArray();
        return copy;
    }

    internal static IEnumerable<T> BrowseFolders<T>(SearchFilter searchFilter, Func<IHierarchyProperty, T> selector)
    {
        // We are not concerned with assets being added multiple times as we only show the contents
        // of each selected folder. This is an issue when searching recursively into child folders.
        HierarchyProperty property;
        foreach (string folderPath in searchFilter.folders)
        {
            if (folderPath == UnityEditor.PackageManager.Folders.GetPackagesPath())
            {
                var packages = PackageManagerUtilityInternal.GetAllVisiblePackages(searchFilter.skipHidden);
                foreach (var package in packages)
                {
                    var packageFolderInstanceId = AssetDatabase.GetMainAssetOrInProgressProxyInstanceID(package.assetPath);
                    property = new HierarchyProperty(package.assetPath);
                    if (property.Find(packageFolderInstanceId, null))
                    {
                        FilteredHierarchy.FilterResult result = new FilteredHierarchy.FilterResult();
                        var copy = CopyPropertyData(property);
                        copy.name = !string.IsNullOrEmpty(package.displayName) ? package.displayName : package.name;
                        yield return selector(property);
                    }
                }
                continue;
            }

            if (searchFilter.skipHidden && !PackageManagerUtilityInternal.IsPathInVisiblePackage(folderPath))
                continue;

            int folderInstanceID = AssetDatabase.GetMainAssetOrInProgressProxyInstanceID(folderPath);
            property = new HierarchyProperty(folderPath);
            property.SetSearchFilter(searchFilter);

            int folderDepth = property.depth;
            int[] expanded = { folderInstanceID };
            var subAssets = new Dictionary<string, List<HierarchyPropertyCopy>>();
            var parentAssets = new List<HierarchyPropertyCopy>();
            while (property.Next(expanded))
            {
                if (property.depth <= folderDepth)
                    break; // current property is outside folder

                var copy = CopyPropertyData(property);
                //list.Add(result);

                // Fetch sub assets by expanding the main asset (ignore folders)
                if (property.hasChildren && !property.isFolder)
                {
                    parentAssets.Add(copy);
                    var subAssetList = new List<HierarchyPropertyCopy>();
                    subAssets.Add(copy.guid, subAssetList);
                    System.Array.Resize(ref expanded, expanded.Length + 1);
                    expanded[expanded.Length - 1] = property.instanceID;
                }
                else
                {
                    if (subAssets.TryGetValue(copy.guid, out var subAssetList))
                    {
                        subAssetList.Add(copy);
                        subAssets[copy.guid] = subAssetList;
                    }
                    else
                    {
                        parentAssets.Add(copy);
                    }
                }
            }
            parentAssets.Sort((result1, result2) => EditorUtility.NaturalCompare(result1.name, result2.name));
            foreach (var result in parentAssets)
            {
                yield return selector(result);
                if (subAssets.TryGetValue(result.guid, out var subAssetList))
                {
                    subAssetList.Sort((result1, result2) => EditorUtility.NaturalCompare(result1.name, result2.name));
                    foreach (var subasset in subAssetList)
                    {
                        yield return selector(subasset);
                    }
                }
            }
        }
    }

    static IEnumerable<IHierarchyProperty> EnumerateAllAssets(SearchFilter searchFilter)
    {
        var rIt = AssetDatabase.EnumerateAllAssets(searchFilter);
        while (rIt.MoveNext())
        {
            yield return CopyPropertyData(rIt.Current);
        }
    }

    static IEnumerable<IHierarchyProperty> EnumeratePaths(SearchFilter searchFilter)
    {
        if (searchFilter.GetState() == SearchFilter.State.FolderBrowsing)
            return BrowseFolders(searchFilter, p => p);
        return EnumerateAllAssets(searchFilter);
    }

    static bool EmptyQuery(SearchContext context)
    {
        return string.IsNullOrEmpty(context.searchQuery) && context.userData is not SearchFilter;
    }

    static void SetupQueryEngine()
    {
        var validationOptions = new QueryValidationOptions() { skipIncompleteFilters = false, skipNestedQueries = true, skipUnknownFilters = true, validateFilters = true, validateSyntaxOnly = true };
        s_QueryEngine = new QueryEngine(validationOptions);
        s_QueryEngine.SetSearchDataCallback(o => null);
        s_FolderFilter = s_QueryEngine.SetFilter<string>("folder", o => null, new[] { ":" })
            .AddOrUpdatePropositionData("Folder", null, "folder=Assets", "Find files inside a folder. If this is the only token, then it acts as a folder browser.");
    }

    static SearchFilter ConvertToSearchFilter(SearchContext context)
    {
        var flags = context.options;
        var searchQuery = context.searchQuery;
        var searchFilter = new SearchFilter
        {
            searchArea = GetSearchArea(flags),
            showAllHits = flags.HasAny(SearchFlags.WantsMore),
            originalText = searchQuery
        };
        if (!string.IsNullOrEmpty(searchQuery))
        {
            var query = s_QueryEngine.ParseQuery(searchQuery);
            if (HasFolderFilter(query, out var filterNode))
            {
                searchFilter.folders = new[] { filterNode.filterValue };
                searchQuery = searchQuery.Replace(filterNode.identifier, "").Trim();
                searchFilter.searchArea = SearchFilter.SearchArea.SelectedFolders;

                if (flags.HasAny(SearchFlags.HierarchicalResults) && string.IsNullOrWhiteSpace(searchQuery))
                    searchQuery = "glob:\"**\"";
            }

            SearchUtility.ParseSearchString(searchQuery, searchFilter);
        }
        if (context.filterType != null && searchFilter.classNames.Length == 0)
            searchFilter.classNames = new[] { context.filterType.Name };

        return searchFilter;
    }

    static bool HasFolderFilter<T>(ParsedQuery<T> query, out IFilterNode filter)
    {
        var graph = query.queryGraph;
        filter = graph.EnumerateNodes().FirstOrDefault(node => node is IFilterNode fn && fn.filterId == s_FolderFilter.token) as IFilterNode;
        return filter != null;
    }

    static IEnumerable<SearchItem> FetchItems(SearchContext context, SearchProvider provider)
    {
        if (EmptyQuery(context))
            yield break;

        if (s_QueryEngine == null)
            SetupQueryEngine();

        var searchFilter = context.userData as SearchFilter;
        if (searchFilter == null)
            searchFilter = ConvertToSearchFilter(context);

        // Search asset database
        foreach (var prop in EnumeratePaths(searchFilter))
        {
            var score = 0;
            if (prop.isFolder)
                score -= 1000;
            string path = null;
            if (prop.pptrValue)
                path = AssetDatabase.GetAssetPath(prop.instanceID);

            if (context.options.HasAny(SearchFlags.HierarchicalResults))
            {
                var item = provider.CreateItem(context, path ?? prop.guid, score, prop.name, "", prop.icon, prop);
                if (!string.IsNullOrEmpty(path))
                    item.parentId = Utils.GetFolderFromPath(path);
                yield return item;
            }
            else
                yield return provider.CreateItem(context, path ?? prop.guid, score, prop.name, "", prop.icon, prop);
            // yield return AssetProvider.CreateItem("FS", context, provider, prop.pptrValue?.GetType(), null, path, score, SearchDocumentFlags.Asset);
        }
    }

    static IEnumerable<SearchProposition> FetchPropositions(SearchContext context, SearchPropositionOptions options)
    {
        if (!options.flags.HasAny(SearchPropositionFlags.QueryBuilder))
            yield break;

        if (s_QueryEngine == null)
            SetupQueryEngine();

        foreach (var proposition in s_QueryEngine.GetPropositions(QueryEnginePropositionsExtension.CombiningOperatorPropositions.None))
            yield return proposition;

        foreach (var f in QueryListBlockAttribute.GetPropositions(typeof(QueryTypeBlock)))
            yield return f;
        foreach (var f in QueryListBlockAttribute.GetPropositions(typeof(QueryLabelBlock)))
            yield return f;
        foreach (var f in QueryListBlockAttribute.GetPropositions(typeof(QueryAreaFilterBlock)))
            yield return f;
        foreach (var f in QueryListBlockAttribute.GetPropositions(typeof(QueryBundleFilterBlock)))
            yield return f;

        yield return new SearchProposition(category: null, "Reference", "ref:<$object:none,UnityEngine.Object$>", "Find all assets referencing a specific asset.");
        yield return new SearchProposition(category: null, "Glob", "glob:\"Assets/**/*.png\"", "Search according to a glob query.");
    }

    static Texture2D FetchPreview(SearchItem item, SearchContext context, Vector2 size, FetchPreviewOptions options)
    {
        Texture2D preview = null;
        if (item.data is not IHierarchyProperty prop)
            return null;
        var clientId = context.searchView != null ? context.searchView.GetViewId() : 0;
        if (prop.instanceID != 0)
        {
            preview = AssetPreview.GetAssetPreview(prop.instanceID, clientId);
            if (preview)
                return preview;
            if (AssetPreview.IsLoadingAssetPreview(prop.instanceID, clientId))
                return null;
        }
        else if (!string.IsNullOrEmpty(prop.guid))
            preview = AssetPreview.GetAssetPreviewFromGUID(prop.guid, clientId);

        if (preview == null || !preview)
            preview = prop.icon;
        return preview;
    }

    static IEnumerable<SearchBadgePosition> HasBadges(SearchItem item, SearchContext context, SearchBadgeTypes badgeTypes)
    {
        if (!badgeTypes.HasAny(SearchBadgeTypes.AssetType) ||
            item.data is not IHierarchyProperty prop ||
            prop.isFolder)
            return Enumerable.Empty<SearchBadgePosition>();

        return new[] { SearchBadgePosition.Grid_LowerRight };
    }

    static SearchBadge FetchBadge(SearchItem item, SearchContext context, SearchBadgePosition position, SearchBadgeTypes types)
    {
        if (item.data is not IHierarchyProperty prop)
            return SearchBadge.invalid;

        if (!types.HasAny(SearchBadgeTypes.AssetType) || position != SearchBadgePosition.Grid_LowerRight)
            return SearchBadge.invalid;

        var gid = GlobalObjectId.GetGlobalObjectIdSlow(prop.instanceID);
        Texture2D icon = null;
        var assetType = prop.pptrValue?.GetType();
        if (gid.identifierType == (int)GlobalObjectId.IdentifierType.BuiltInAsset)
        {
            icon = AssetPreview.GetMiniThumbnail(prop.pptrValue);
        }
        else
        {
            icon = UnityEditor.Search.SearchUtils.GetTypeIcon(assetType ?? typeof(GameObject));
        }
        if (icon == item.preview)
        {
            icon = noBadge;
        }
        return new SearchBadge(icon, assetType?.Name, position, types);
    }

    static int ToInstanceId(SearchItem item)
    {
        if (item.data is not IHierarchyProperty prop)
            return 0;

        return prop.instanceID;
    }

    static Object ToObject(SearchItem item, Type type)
    {
        if (item.data is not IHierarchyProperty prop)
            return null;

        if (!prop.pptrValue)
            return null;

        if (type == null)
            return prop.pptrValue;
        var objType = prop.pptrValue.GetType();
        if (type.IsAssignableFrom(objType))
            return prop.pptrValue;

        if (prop.pptrValue is GameObject go && typeof(Component).IsAssignableFrom(type))
            return go.GetComponent(type);

        return null;
    }

    [SearchItemProvider]
    internal static SearchProvider CreateProvider()
    {
        return new SearchProvider(type, "File System")
        {
            type = "asset",
            active = false,
            priority = 2500,
            isExplicitProvider = true,
            // fetchDescription => FetchDescription
            fetchItems = (context, items, provider) => FetchItems(context, provider),
            fetchPropositions = (context, options) => FetchPropositions(context, options),
            fetchPreview = FetchPreview,
            startDrag = (item, context) => StartDrag(item, context),
            hasBadges = HasBadges,
            fetchBadge = FetchBadge,
            toInstanceId = ToInstanceId,
            toObject = ToObject
        };
    }
    
    private static void StartDrag(SearchItem item, SearchContext context)
    {
        if (context.selection.Count > 1)
        {
            var selectedObjects = context.selection.Select(i => ToObject(i, typeof(Object))).Where(obj => obj != null);
            var paths = selectedObjects.Select(obj => AssetDatabase.GetAssetPath(obj)).Where(p => !string.IsNullOrEmpty(p)).ToArray();
            Utils.StartDrag(selectedObjects.ToArray(), paths, item.GetLabel(context, true));
        }
        else
        {
            var obj = ToObject(item, typeof(Object));
            if (obj)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                Utils.StartDrag(new[] { obj }, new[] { path }, item.GetLabel(context, true));
            }
        }
    }

    static void OpenItem(SearchItem item)
    {
        if (item.data is not IHierarchyProperty prop)
            return;

        var gid = GlobalObjectId.GetGlobalObjectIdSlow(prop.instanceID);
        if (gid.identifierType == (int)GlobalObjectId.IdentifierType.SceneObject)
        {
            var guid = gid.assetGUID.ToString();
            var source = AssetDatabase.GUIDToAssetPath(guid);
            var containerAsset = AssetDatabase.LoadAssetAtPath<Object>(source);
            if (containerAsset != null)
            {
                AssetDatabase.OpenAsset(containerAsset);
            }
        }

        var asset = ToObject(item, typeof(UnityEngine.Object));
        if (asset == null || !AssetDatabase.OpenAsset(asset))
            EditorUtility.OpenWithDefaultApp(AssetDatabase.GetAssetPath(asset));
    }

    [SearchActionsProvider]
    internal static IEnumerable<SearchAction> CreateActionHandlers()
    {
        return new[]
        {
            new SearchAction(type, "open", null, "Open", OpenItem) { enabled = items => items.Count == 1 },
        };
    }
}
