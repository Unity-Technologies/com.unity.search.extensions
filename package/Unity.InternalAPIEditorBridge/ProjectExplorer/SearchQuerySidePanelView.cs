using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Search;
using UnityEngine.UIElements;
using UnityEditor.Search;
using System;
using UnityEditor;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;

readonly struct SearchQueryNodeData
{
    public SearchQueryNodeData(bool isRoot, int treeNodeId, int handlerId, int queryId, object data = null)
    {
        this.treeNodeId = treeNodeId;
        this.handlerId = handlerId;
        this.queryId = queryId;
        this.data = data;
        this.isRoot = isRoot;
    }
    // TODO: Should it be a reference to the actual handler?
    public readonly bool isRoot;
    public readonly int treeNodeId;
    public readonly int handlerId;
    public readonly int queryId;
    public readonly object data;

    public static TreeViewItemData<SearchQueryNodeData> CreateItemData(bool isRoot, int treeNodeId, int handlerId, int queryId, object data = null, List<TreeViewItemData<SearchQueryNodeData>> children = null)
    {
        return new TreeViewItemData<SearchQueryNodeData>(treeNodeId, new SearchQueryNodeData(isRoot, treeNodeId, handlerId, queryId, data), children);
    }

    public static TreeViewItemData<SearchQueryNodeData> CreateItemData(int treeNodeId, int handlerId, int queryId, object data = null, List<TreeViewItemData<SearchQueryNodeData>> children = null)
    {
        return CreateItemData(false, treeNodeId, handlerId, queryId, data, children);
    }

    public static TreeViewItemData<SearchQueryNodeData> CreateRootData(int treeNodeId, int handlerId, int queryId, object data = null, List<TreeViewItemData<SearchQueryNodeData>> children = null)
    {
        return CreateItemData(true, treeNodeId, handlerId, queryId, data, children);
    }
}

interface ISearchQueryNodeHandler
{
    public int id { get; }
    IEnumerable<TreeViewItemData<SearchQueryNodeData>> GetRoots(SearchContext context);
    // Should support for operation be more generic and should we have an enum for the various operations? Rename, Delete, Save
    bool supportsRename { get; }
    void Rename(TreeView tree, SearchQueryTreeViewItem e);
    public void PopulateContextualMenu(TreeView tree, SearchContext context, SearchQueryTreeViewItem item, DropdownMenu menu);
    public void ActivateItem(TreeView tree, SearchQueryTreeViewItem item);
    public void BindItem(TreeView tree, SearchQueryTreeViewItem item, int itemIndex);
}

abstract class BaseSearchQueryNodeHandler : ISearchQueryNodeHandler
{
    protected static readonly string k_SaveMenuLabel = L10n.Tr("Save");
    protected static readonly string k_OpenInNewWindowMenuLabel = L10n.Tr("Open in new window");
    protected static readonly string k_RenameMenuLabel = L10n.Tr("Rename");
    protected static readonly string k_SetIconMenuLabel = L10n.Tr("Set Icon...");
    protected static readonly string k_SearchTemplateMenuLabel = L10n.Tr("Search Template");
    protected static readonly string k_DeleteMenuLabel = L10n.Tr("Delete");
    protected static readonly string k_EditInInspectorMenuLabel = L10n.Tr("Edit in Inspector");

    public abstract string name { get; }
    public bool supportsRename => true;

    public virtual int id => throw new NotImplementedException();

    public virtual void PopulateContextualMenu(TreeView tree, SearchContext context, SearchQueryTreeViewItem item, DropdownMenu menu)
    {
        var query = GetQuery(item);
        if (item.viewState.activeQuery == query && !context.empty)
        {
            menu.AppendAction(k_SaveMenuLabel, (_) => item.Emit(SearchEvent.SaveActiveSearchQuery));
            menu.AppendSeparator();
        }
        menu.AppendAction(k_OpenInNewWindowMenuLabel, (action) =>
        {
            SearchQuery.Open(query, SearchFlags.None);
        });
        menu.AppendSeparator();
        menu.AppendAction(k_RenameMenuLabel, (_) => Rename(tree, item));
    }

    public abstract ISearchQuery GetQuery(SearchQueryTreeViewItem item);
    public abstract IEnumerable<TreeViewItemData<SearchQueryNodeData>> GetRoots(SearchContext context);
    public abstract void Rename(TreeView tree, SearchQueryTreeViewItem e);

    public void ActivateItem(TreeView tree, SearchQueryTreeViewItem item)
    {
        var query = GetQuery(item);
        if (query == null)
            return;
        item.Emit(SearchEvent.ExecuteSearchQuery, query);
    }

    public void BindItem(TreeView tree, SearchQueryTreeViewItem item, int itemIndex)
    {
        if (item.data.isRoot)
        {
            // Root item
            item.Bind(null, name);
            return;
        }
        var query = GetQuery(item);
        item.Bind(SearchQuery.GetIcon(query), query.displayName, query.itemCount);
    }

    public IEnumerable<ISearchQuery> GetValidQueries(IEnumerable<ISearchQuery> queries, IEnumerable<SearchProvider> providers)
    {
        var unallowedProviders = SearchService.GetActiveProviders().Where(p1 => !providers.Any(p2 => p1.id == p2.id));
        var unallowedFilterIds = new List<string>(unallowedProviders.Select(p => p.filterId));

        foreach(var q in queries)
        {
            if (unallowedFilterIds.Any(filterId => q.searchText.StartsWith(filterId)))
                continue;

            if (providers.Any(p => q.searchText.StartsWith(p.filterId)))
            {
                yield return q;
            }
            else
            {
                var queryProviders = q.GetProviderIds();
                if (!queryProviders.Any())
                {
                    yield return q;
                }
                else if (queryProviders.Any(qpid => providers.FirstOrDefault(p => p.id == qpid) != null))
                {
                    yield return q;
                }
            }
            
        }
    }
}

class UserSearchQueryNodeHandler : BaseSearchQueryNodeHandler
{
    List<ISearchQuery> m_Queries;
    public override string name => "User Collections";
    public override int id => nameof(UserSearchQueryNodeHandler).GetHashCode();

    public UserSearchQueryNodeHandler()
    {
    }

    public override ISearchQuery GetQuery(SearchQueryTreeViewItem item)
    {
        var index = item.data.queryId;
        if (index < 0 || index >= m_Queries.Count)
            return null;
        return m_Queries[index];
    }

    public override IEnumerable<TreeViewItemData<SearchQueryNodeData>> GetRoots(SearchContext context)
    {
        if (m_Queries == null)
        {
            m_Queries = GetValidQueries(SearchQuery.userQueries, context.GetProviders()).ToList();
        }
        
        int queryIndex = 0;
        var items = m_Queries.Select(q => {
            return SearchQueryNodeData.CreateItemData(q.guid.GetHashCode(), id, queryIndex++);
        }).ToList();

        yield return SearchQueryNodeData.CreateRootData(GUID.Generate().GetHashCode(), id, -1, null, items);
    }

    public override void PopulateContextualMenu(TreeView tree, SearchContext context, SearchQueryTreeViewItem item, DropdownMenu menu)
    {
        base.PopulateContextualMenu(tree, context, item, menu);

        var query = GetQuery(item);

        menu.AppendAction(k_SetIconMenuLabel, (_) => UnityEditor.Search.SearchUtils.ShowIconPicker((newIcon, canceled) =>
        {
            if (canceled)
                return;
            query.thumbnail = newIcon;
            SearchQuery.SaveSearchQuery((SearchQuery)query);
        }));
        menu.AppendAction(k_SearchTemplateMenuLabel, (_) => ((SearchQuery)query).isSearchTemplate = !query.isSearchTemplate, action =>
        {
            if (query.isSearchTemplate)
                return DropdownMenuAction.Status.Checked;
            return DropdownMenuAction.Status.Normal;
        });
        menu.AppendAction(Utils.GetRevealInFinderLabel(), (_) => EditorUtility.RevealInFinder(query.filePath));
        menu.AppendSeparator();
        menu.AppendAction(k_DeleteMenuLabel, (_) =>
        {
            if (item.viewState.activeQuery == query)
                item.viewState.activeQuery = null;
            SearchQuery.RemoveSearchQuery((SearchQuery)query);
        });
    }

    public override void Rename(TreeView tree, SearchQueryTreeViewItem e)
    {
        throw new NotImplementedException();
    }
}

class ProjectSearchQueryNodeHandler : BaseSearchQueryNodeHandler
{
    List<SearchQueryAsset> m_Queries;

    public override string name => "Project Collections";
    public override int id => nameof(ProjectSearchQueryNodeHandler).GetHashCode();

    public ProjectSearchQueryNodeHandler()
    {
    }

    public override ISearchQuery GetQuery(SearchQueryTreeViewItem item)
    {
        var index = item.data.queryId;
        if (index < 0 || index >= m_Queries.Count)
            return null;
        return m_Queries[index];
    }

    public override IEnumerable<TreeViewItemData<SearchQueryNodeData>> GetRoots(SearchContext context)
    {
        if (m_Queries == null)
        {
            m_Queries = GetValidQueries(SearchQueryAsset.savedQueries.Cast<ISearchQuery>(), context.GetProviders()).Cast<SearchQueryAsset>().ToList();
        }

        int queryIndex = 0;
        var items = m_Queries.Select(q =>
        {
            return SearchQueryNodeData.CreateItemData(q.guid.GetHashCode(), id, queryIndex++);
        }).ToList();

        yield return SearchQueryNodeData.CreateRootData(GUID.Generate().GetHashCode(), id, -1, null, items);
    }

    public override void PopulateContextualMenu(TreeView tree, SearchContext context, SearchQueryTreeViewItem item, DropdownMenu menu)
    {
        base.PopulateContextualMenu(tree, context, item, menu);

        var query = GetQuery(item);

        menu.AppendAction(k_SetIconMenuLabel, (_) => UnityEditor.Search.SearchUtils.ShowIconPicker((newIcon, canceled) =>
        {
            if (canceled)
                return;
            query.thumbnail = newIcon;
            // TODO
            // SearchQuery.SaveSearchQuery((SearchQuery)query);
        }));
        menu.AppendAction(k_SearchTemplateMenuLabel, (_) => ((SearchQuery)query).isSearchTemplate = !query.isSearchTemplate, action =>
        {
            if (query.isSearchTemplate)
                return DropdownMenuAction.Status.Checked;
            return DropdownMenuAction.Status.Normal;
        });
        menu.AppendAction(Utils.GetRevealInFinderLabel(), (_) => EditorUtility.RevealInFinder(query.filePath));
        menu.AppendSeparator();
        menu.AppendAction(k_DeleteMenuLabel, (_) =>
        {
            if (item.viewState.activeQuery == query)
                item.viewState.activeQuery = null;
            SearchQuery.RemoveSearchQuery((SearchQuery)query);
        });
    }

    public override void Rename(TreeView tree, SearchQueryTreeViewItem e)
    {
        throw new NotImplementedException();
    }
}

class FolderInfo
{
    public FolderInfo(string path, ISearchQuery query)
    {
        this.path = path;
        this.query = query;
    }

    public ISearchQuery query;
    public bool childrenPopulated;
    public string path;
}

class LightWeightSearchQuery : ISearchQuery
{
    public string searchText { get; private set; }
    public string displayName { get; set; }
    public string details { get; set; }
    public Texture2D thumbnail { get; set; }
    public string filePath => throw new NotImplementedException();
    public string guid { get; private set; }

    public long creationTime => throw new NotImplementedException();
    public long lastUsedTime => throw new NotImplementedException();
    public int itemCount => throw new NotImplementedException();
    public bool isSearchTemplate => throw new NotImplementedException();

    private string[] m_ProviderIds;

    public static LightWeightSearchQuery Create(string displayName, string searchText, string[] providerIds, string guid = null)
    {
        return new LightWeightSearchQuery()
        {
            guid = guid ?? GUID.Generate().ToString(),
            displayName= displayName,
            searchText = searchText,
            m_ProviderIds = providerIds
        };
    }

    public string GetName()
    {
        return displayName;
    }

    public IEnumerable<string> GetProviderIds()
    {
        return m_ProviderIds;
    }

    public IEnumerable<string> GetProviderTypes()
    {
        throw new NotImplementedException();
    }

    public SearchTable GetSearchTable()
    {
        return null;
    }

    public SearchViewState GetViewState()
    {
        return null;
    }
}

class FileSystemNodeHandler : ISearchQueryNodeHandler
{
    class Styles
    {
        public static Texture2D folder = EditorGUIUtility.FindTexture("Folder Icon");
        public static Texture2D folderOpen = EditorGUIUtility.FindTexture("FolderOpened Icon");
        public static Texture2D folderEmpty = EditorGUIUtility.FindTexture("FolderEmpty Icon");
    }

    public int id => nameof(FileSystemNodeHandler).GetHashCode();

    public bool supportsRename => throw new NotImplementedException();

    public void ActivateItem(TreeView tree, SearchQueryTreeViewItem item)
    {
        // throw new NotImplementedException();
        var info = (FolderInfo)item.data.data;

        // TODO Fast Refresh: should we package the sync query into the payload?
        item.EmitSync(SearchEvent.ExecuteSearchQuery, info.query);
    }

    public void BindItem(TreeView tree, SearchQueryTreeViewItem item, int itemIndex)
    {
        var treeNodeData = item.data;
        var state = (FolderInfo)treeNodeData.data;
        var name = Path.GetFileName(state.path);
        item.Bind(tree.IsExpanded(item.data.treeNodeId) ? Styles.folderOpen : Styles.folder, name);
    }

    public IEnumerable<TreeViewItemData<SearchQueryNodeData>> GetRoots(SearchContext context)
    {
        var packages = AssetDatabase.GetAssetRootFolders().Where(path => path.StartsWith("Packages/")).ToList();
        yield return GetFolderItem(true, context, "Assets");
        yield return GetFolderItem(true, context, "Packages", packages);
    }

    private TreeViewItemData<SearchQueryNodeData> GetFolderItem(bool isRoot, SearchContext context, string path, List<string> childDirNames = null)
    {
        if (!Directory.Exists(path))
            throw new Exception($"Path does not exists or is not a directory {path}");

        var dirInfo = new DirectoryInfo(path);
        childDirNames = childDirNames ?? Directory.EnumerateDirectories(path).ToList();
        var childDirItems = childDirNames.Select(p =>
        {
            return GetFolderItem(false, context, p);
        }).ToList();
        var query = CreateListFolderQuery(path);
        return SearchQueryNodeData.CreateItemData(isRoot, query.guid.GetHashCode(), id, query.guid.GetHashCode(), new FolderInfo(path, query), childDirItems);
    }

    static Regex s_CaptureFolder = new Regex("folder=\"(.*)\"");

    public static bool TryGetFolderQuery(string query, out string folder)
    {
        folder = null;
        var match = s_CaptureFolder.Match(query);
        if (match.Success)
        {
            folder = match.Groups[1].Value;
        }
        return folder != null;
    }

    public static ISearchQuery CreateListFolderQuery(string folder)
    {
        // Need to clear the path to ensure proper id generation.
        folder = folder.Replace("\\", "/");
        var text = $"{FileSystemProvider.type}:folder=\"{folder}\"";
        var query = LightWeightSearchQuery.Create(Path.GetFileName(folder), text, new[] { "fs" }, text);
        return query;
    }

    public void PopulateContextualMenu(TreeView tree, SearchContext context, SearchQueryTreeViewItem item, DropdownMenu menu)
    {
        throw new NotImplementedException();
    }

    public void Rename(TreeView tree, SearchQueryTreeViewItem e)
    {
        throw new NotImplementedException();
    }
}

class SearchQueryTreeViewItem : SearchElement
{
    RenamableLabel m_Label;
    VisualElement m_Icon;
    Label m_CountLabel;
    IManipulator m_ContextualMenuManipulator;
    public ISearchQueryNodeHandler handler { get; set; }
    public SearchQueryNodeData data { get; set; }

    internal static readonly string tabCountTextColorFormat = EditorGUIUtility.isProSkin ? "<color=#7B7B7B>{0}</color>" : "<color=#6A6A6A>{0}</color>";

    public static readonly string ussClassName = "search-query-treeview-item";
    public static readonly string listItemClassName = "search-query-listview-item";
    public static readonly string nameLabelClassName = listItemClassName.WithUssElement("label");
    public static readonly string ussListViewClassName = "search-query-listview";
    public static readonly string ussTreeViewClassName = "search-query-treeview";
    public static readonly string headerClassName = ussTreeViewClassName.WithUssElement("header");
    public static readonly string countLabelClassName = listItemClassName.WithUssElement("count");

    public SearchQueryTreeViewItem(ISearchView viewModel, params string[] classes)
        : base(nameof(SearchQueryTreeViewItem), viewModel, classes)
    {
        AddToClassList(ussClassName);
        m_Label = new RenamableLabel();
        m_Label.AddToClassList(nameLabelClassName);
        // m_Label.renameFinished += HandleRenameFinished;
        m_Icon = new VisualElement();
        m_Icon.AddToClassList(SearchQueryPanelView.iconClassName);
        m_CountLabel = new Label();
        m_CountLabel.AddToClassList(countLabelClassName);
        m_CountLabel.pickingMode = PickingMode.Ignore;

        style.flexDirection = FlexDirection.Row;
        Add(m_Icon);
        Add(m_Label);
        Add(m_CountLabel);

        m_ContextualMenuManipulator = new ContextualMenuManipulator(HandleContextualMenu);
        this.AddManipulator(m_ContextualMenuManipulator);
        RegisterCallback<PointerDownEvent>(OnPointerDown);
    }

    public string text => m_Label.text;

    public void Bind(Texture2D icon, string label, int count = -1)
    {
        m_Icon.style.display = icon == null ? DisplayStyle.None : DisplayStyle.Flex;
        if (icon != null)
        {
            m_Icon.style.backgroundImage = new StyleBackground(icon);
        }
        m_Label.text = label;

        m_CountLabel.style.display = count < 0 ? DisplayStyle.None : DisplayStyle.Flex;
        m_CountLabel.text = count.ToString();
    }

    protected virtual void HandleContextualMenu(ContextualMenuPopulateEvent evt)
    {
        // Can it be implemented at the tree level
        // m_Handler.PopulateContextualMenu(this, evt.menu);
    }

    void OnPointerDown(PointerDownEvent evt)
    {
        /*
        if (evt.clickCount != 1 || evt.button != 0)
            return;

        if (!m_Selected)
        {
            m_Selected = true;
            return;
        }

        if (m_Selected && !m_IsRenaming)
        {
            Rename();
            evt.StopImmediatePropagation();
            evt.PreventDefault();
        }
        */
    }
}

class SearchQuerySidePanelView : SearchElement
{
    private TreeView m_TreeView;
    private List<ISearchQueryNodeHandler> m_Handlers;
    private List<Action> m_SearchEventOffs;

    public SearchQuerySidePanelView(string name, ISearchView viewModel, params string[] classes)
        : base(name, viewModel, classes)
    {
        m_TreeView = CreateSearchQueryTreeView();
        Add(m_TreeView);

        AddStyleSheetPath(ProjectExplorerWindow.kProjectExplorerStyleSheet);

        m_Handlers = new List<ISearchQueryNodeHandler>();
        m_Handlers.Add(new UserSearchQueryNodeHandler());
        m_Handlers.Add(new ProjectSearchQueryNodeHandler());
        m_Handlers.Add(new FileSystemNodeHandler());

        var roots = m_Handlers.SelectMany(h => h.GetRoots(viewModel.context)).ToList();
        m_TreeView.SetRootItems(roots);

        m_SearchEventOffs = new List<Action>()
        {
            On(SearchEvent.SearchQueryExecuted, HandleQueryExecuted),
        };
    }

    protected override void OnAttachToPanel(AttachToPanelEvent evt)
    {
        base.OnAttachToPanel(evt);

        EditorApplication.delayCall += SetupInitialQuery;
    }

    protected override void OnDetachFromPanel(DetachFromPanelEvent evt)
    {
        base.OnDetachFromPanel(evt);
        m_SearchEventOffs.ForEach(off => off());
    }

    void HandleQueryExecuted(ISearchEvent evt)
    {
        var query = evt.GetArgument<ISearchQuery>(0);
        if (query != null)
        {
            var queryId = query.guid.GetHashCode();
            m_TreeView.SetSelectionByIdWithoutNotify(new[] { queryId });
            m_TreeView.ScrollToItemById(queryId);
        }
    }

    void SetupInitialQuery()
    {
        // TODO: this is super hackish. We need a better way to set the initial QUERY.
        EditorApplication.delayCall -= SetupInitialQuery;
        if (!m_TreeView.hasActiveItems)
        {
            EditorApplication.delayCall += SetupInitialQuery;
            return; 
        }

        var roots = m_TreeView.GetRootIds();
        foreach (var rootId in roots)
        {
            var rootItem = m_TreeView.GetRootElementForId(rootId);
            if (rootItem != null)
            {
                var queryItem = rootItem.Q<SearchQueryTreeViewItem>();
                if (queryItem != null && queryItem.text == "Assets")
                {
                    queryItem.handler.ActivateItem(m_TreeView, queryItem);
                    break;
                }
            }
        }
    }

    public TreeView CreateSearchQueryTreeView()
    {
        var treeView = new TreeView();
        treeView.bindItem = BindItem;
        treeView.makeItem = MakeItem;
        treeView.selectionType = SelectionType.Single;
        treeView.selectedIndicesChanged += HandleItemsSelected;
        // TODO: no message when expanding? Load full tree for now. Maybe we should use Activate to lazy expand?
        return treeView;
    }

    public void HandleItemsSelected(IEnumerable<int> indices)
    {
        if (indices.Any())
        {
            var itemIndex = indices.First();
            var item = m_TreeView.GetRootElementForIndex(itemIndex).Q<SearchQueryTreeViewItem>();
            item.handler.ActivateItem(m_TreeView, item);
        }
    }

    public SearchQueryTreeViewItem MakeItem()
    {
        return new SearchQueryTreeViewItem(m_ViewModel);
    }

    public void BindItem(VisualElement e, int index)
    {
        var item = (SearchQueryTreeViewItem)e;
        var data = m_TreeView.GetItemDataForIndex<SearchQueryNodeData>(index);
        var handler = m_Handlers.First(h => h.id == data.handlerId);
        item.data = data;
        item.handler = handler;
        if (item.data.isRoot)
        {
            item.AddToClassList(SearchQueryTreeViewItem.headerClassName);
        }
        else
        {
            item.RemoveFromClassList(SearchQueryTreeViewItem.headerClassName);
        }

        handler.BindItem(m_TreeView, item, index);
    }

    ISearchQueryNodeHandler GetHandler(SearchQueryNodeData data)
    {
        return m_Handlers.First(h => h.id == data.handlerId);
    }
}

