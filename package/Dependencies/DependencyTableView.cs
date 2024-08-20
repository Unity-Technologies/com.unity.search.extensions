using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using System.Linq;
using UnityEditorInternal.VersionControl;

namespace UnityEditor.Search
{
    abstract class BaseDependencyTableView : ITableView
    {
        readonly HashSet<SearchItem> m_Items;
        public readonly DependencyState state;

        public SearchContext context => state.context;
        public IDependencyViewHost host { get; private set; }
        public bool empty => m_Items == null ? true : m_Items.Count == 0;
        public IEnumerable<SearchItem> items => m_Items;

        protected BaseDependencyTableView(DependencyState state, IDependencyViewHost host)
        {
            this.host = host;
            this.state = state;
            m_Items = new HashSet<SearchItem>();
            Reload();
        }

        #region UIBackendSpecific Overridables
        public virtual void OnGUI(Rect rect)
        {
        }

        protected virtual void BuildTable()
        {
            throw new NotImplementedException();
        }

        protected virtual void AddToItemContextualMenu(GenericMenu menu, SearchItem item)
        {
            var itemName = System.IO.Path.GetFileName(item.GetLabel(context, true));
            menu.AddDisabledItem(new GUIContent(itemName), false);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Select"), false, () => SelectObject(item));
            if (!SearchSettings.searchItemFavorites.Contains(item.id))
                menu.AddItem(new GUIContent("Add to favorite"), false, () => AddFavorite(item));
            else
                menu.AddItem(new GUIContent("Remove from favorite"), false, () => RemoveFavorite(item));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Copy GUID"), false, () => CopyGUID(item));
            menu.AddItem(new GUIContent("Copy/Relative Path"), false, () => CopyRelativePath(item));
            menu.AddItem(new GUIContent("Copy/Absolute Path"), false, () => CopyAbsolutePath(item));
            menu.AddSeparator("");

            PopulateActionInSearchMenu(menu, item);
        }

        #endregion

        #region ITableView
        public void Reload()
        {
            m_Items.Clear();
            SearchService.Request(state.context, (c, items) => m_Items.UnionWith(items), _ => BuildTable());
        }

#if USE_SEARCH_EXTENSION_API
        public bool readOnly => false;
#else
         public bool IsReadOnly()
        {
            return false;
        }
#endif

        public IEnumerable<SearchItem> GetElements()
        {
            return m_Items;
        }

        public IEnumerable<SearchColumn> GetColumns()
        {
            return state.tableConfig.columns;
        }

        public void SetDirty()
        {
            host.Repaint();
        }
        #endregion

        #region ITableView Specific NotImplemented
        // ITableView
        public IEnumerable<SearchItem> GetRows() => throw new NotSupportedException();
        public SearchTable GetSearchTable() => throw new NotSupportedException();
        public void AddColumn(Vector2 mousePosition, int activeColumnIndex)
        {
            throw new NotImplementedException();
        }

        public void AddColumns(IEnumerable<SearchColumn> descriptors, int activeColumnIndex)
        {
            throw new NotImplementedException();
        }

        public void SetupColumns(IEnumerable<SearchItem> elements = null)
        {
            throw new NotImplementedException();
        }

        public void RemoveColumn(int activeColumnIndex)
        {
            throw new NotImplementedException();
        }

        public void SwapColumns(int columnIndex, int swappedColumnIndex)
        {
            throw new NotImplementedException();
        }

        public void SetSelection(IEnumerable<SearchItem> items)
        {
            throw new NotImplementedException();
        }

        public void OnItemExecuted(SearchItem item)
        {
            throw new NotImplementedException();
        }

        public bool OpenContextualMenu(Event evt, SearchItem item)
        {
            throw new NotImplementedException();
        }

        public void UpdateColumnSettings(int columnIndex, MultiColumnHeaderState.Column columnSettings)
        {
            throw new NotImplementedException();
        }

        public bool AddColumnHeaderContextMenuItems(GenericMenu menu)
        {
            throw new NotImplementedException();
        }

        public void AddColumnHeaderContextMenuItems(GenericMenu menu, SearchColumn sourceColumn)
        {
            throw new NotImplementedException();
        }

#if UNITY_2023_1_OR_NEWER
        IEnumerable<object> ITableView.GetValues(int columnIdx)
        {
            throw new NotImplementedException();
        }

        float ITableView.GetRowHeight()
        {
            throw new NotImplementedException();
        }

        int ITableView.GetColumnIndex(string name)
        {
            throw new NotImplementedException();
        }

        SearchColumn ITableView.FindColumnBySelector(string selector)
        {
            throw new NotImplementedException();
        }


#endif
        #endregion

        #region Utility
        internal static void PopulateActionInSearchMenu(GenericMenu menu, SearchItem item)
        {
            var currentSelection = new[] { item };
            foreach (var action in item.provider.actions.Where(a => a.enabled(currentSelection)))
            {
                if (action.id == "select" || action.id == "copy")
                    continue;
                var itemName = !string.IsNullOrWhiteSpace(action.content.text) ? action.content.text : action.content.tooltip;
                menu.AddItem(new GUIContent($"Search/{itemName}"), false, () => ExecuteAction(action, currentSelection));
            }
        }

        internal static void ExecuteAction(SearchAction action, SearchItem[] items)
        {
            // TODO Dep: should go back in DebTableView.

            var item = items.LastOrDefault();
            if (item == null)
                return;

            if (action.handler != null && items.Length == 1)
                action.handler(item);
            else if (action.execute != null)
                action.execute(items);
            else
                action.handler?.Invoke(item);
        }

        internal static void SelectObject(in SearchItem item)
        {
            var obj = item.ToObject();
            if (obj)
                Selection.activeObject = obj;
        }

        internal static string GetAssetPath(in SearchItem item)
        {
            if (item.provider.type == "dep")
                return AssetDatabase.GUIDToAssetPath(item.id);
            return SearchUtils.GetAssetPath(item);
        }

        internal static UnityEngine.Object GetObject(in SearchItem item)
        {
            UnityEngine.Object obj = null;
            var path = GetAssetPath(item);
            if (!string.IsNullOrEmpty(path))
                obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (!obj)
                obj = item.ToObject();
            return obj;
        }

        internal static void TrackSelection(SearchItem item)
        {
            var obj = GetObject(item);
            if (!obj)
                return;
            EditorGUIUtility.PingObject(obj);
        }

        void OpenStateInSearch()
        {
            DependencyViewer.OpenStateInSearch(state);
        }

        static bool TryGetGuid(in SearchItem item, out string guid)
        {
            guid = null;
            var obj = item.ToObject();
            if (!obj)
                return false;
            var gid = GlobalObjectId.GetGlobalObjectIdSlow(obj);
            guid = gid.assetGUID.ToString();
            return true;
        }

        static void CopyAbsolutePath(in SearchItem item)
        {
            if (!TryGetGuid(item, out var guid))
                return;
            var fi = new System.IO.FileInfo(AssetDatabase.GUIDToAssetPath(guid));
            if (!fi.Exists)
                return;
            var fullPath = fi.FullName;
            Debug.Log(fullPath);
            EditorGUIUtility.systemCopyBuffer = fullPath;
        }

        void CopyRelativePath(in SearchItem item)
        {
            var label = item.GetLabel(context, true);
            Debug.Log(label);
            EditorGUIUtility.systemCopyBuffer = label;
        }

        void CopyGUID(in SearchItem item)
        {
            if (!TryGetGuid(item, out var guid))
            {
                CopyRelativePath(item);
            }
            else
            {
                Debug.Log(guid);
                EditorGUIUtility.systemCopyBuffer = guid;
            }
        }

        void RemoveFavorite(in SearchItem item)
        {
            SearchSettings.RemoveItemFavorite(item);
            host.Repaint();
        }

        void AddFavorite(in SearchItem item)
        {
            SearchSettings.AddItemFavorite(item);
            host.Repaint();
        }
        #endregion
    }

#if UNITY_2023_1_OR_NEWER
    // This is wrapper between a SearchView and the actual TableView.
    class DependencyTableView : BaseDependencyTableView
    {
        private SearchTableView m_TableView;   // Actual TableView
        private SearchViewModelEx m_SearchViewModel; // Bound to m_TableView

        public SearchTableView tableView => m_TableView;

        public DependencyTableView(DependencyState state, IDependencyViewHost host)
            : base(state, host)
        {
        }
        #region TableView Overrides
        protected override void BuildTable()
        {
            m_SearchViewModel = new SearchViewModelEx(state.viewState);
            m_SearchViewModel.addToItemContextualMenu = this.AddToItemContextualMenu;
            m_SearchViewModel.trackingCallback = TrackSelection;

            m_SearchViewModel.results.AddItems(items);
            m_TableView = new SearchTableView(m_SearchViewModel);
            m_TableView.style.flexGrow = 1;
        }

        
        #endregion
    }
#else
    class DependencyTableView : BaseDependencyTableView
    {
        public PropertyTable table;
        public DependencyTableView(DependencyState state, IDependencyViewHost host)
            : base(state, host)
        {

        }

        public void ResizeColumns()
        {
            var columns = table.multiColumnHeader.state.columns;
            foreach (var c in columns)
                c.autoResize = false;
            if (columns.Length == 0)
                return;
            columns[Math.Min(columns.Length - 1, 1)].autoResize = true;
            table.multiColumnHeader.ResizeToFit();
        }

        public void AddColumn(Vector2 mousePosition, int activeColumnIndex)
        {
#if USE_SEARCH_MODULE
            var columns = SearchColumn.Enumerate(context, GetElements());
#if USE_SEARCH_EXTENSION_API
            SearchUtils.ShowColumnSelector(AddColumns, columns, mousePosition, activeColumnIndex);
#else
            Utils.CallDelayed(() => ColumnSelector.AddColumns(AddColumns, columns, mousePosition, activeColumnIndex));
#endif
#endif
        }

        public void AddColumns(IEnumerable<SearchColumn> newColumns, int insertColumnAt)
        {
            var columns = new List<SearchColumn>(state.tableConfig.columns);
            if (insertColumnAt == -1)
                insertColumnAt = columns.Count;
            var columnCountBefore = columns.Count;
            columns.InsertRange(insertColumnAt, newColumns);

            var columnAdded = columns.Count - columnCountBefore;
            if (columnAdded > 0)
            {
                state.tableConfig.columns = columns.ToArray();
                BuildTable();
                FrameColumn(insertColumnAt - 1);
            }
        }

        public void SetupColumns(IEnumerable<SearchItem> elements = null)
        {
            BuildTable();
        }

        public void RemoveColumn(int removeColumnAt)
        {
            if (removeColumnAt == -1)
                return;

            var columns = new List<SearchColumn>(state.tableConfig.columns);
            columns.RemoveAt(removeColumnAt);
            state.tableConfig.columns = columns.ToArray();
            BuildTable();
        }

        public void SwapColumns(int columnIndex, int swappedColumnIndex)
        {
            if (swappedColumnIndex == -1)
                return;

            var columns = state.tableConfig.columns;
            var temp = columns[columnIndex];
            columns[columnIndex] = columns[swappedColumnIndex];
            columns[swappedColumnIndex] = temp;
            SetDirty();
        }

        public override void OnGUI(Rect rect)
        {
            table?.OnGUI(rect);
        }

        protected override void BuildTable()
        {
            table = new PropertyTable(state.guid, this);
            ResizeColumns();

            var columnCountIndex = -1;
            var maxWidth = 0f;
            for (int i = 0; i < table.multiColumnHeader.state.columns.Length; i++)
            {
                var c = table.multiColumnHeader.state.columns[i];
                if (c.width > maxWidth)
                {
                    maxWidth = c.width;
                    columnCountIndex = i;
                }
            }
            if (columnCountIndex != -1)
            {
                var content = new GUIContent(table.multiColumnHeader.state.columns[columnCountIndex].headerContent);
                content.text += $" ({m_Items?.Count ?? 0})";
                table.multiColumnHeader.state.columns[columnCountIndex].headerContent = content;
            }
            host.Repaint();
        }

        public void AddColumnHeaderContextMenuItems(GenericMenu menu, SearchColumn sourceColumn)
        {
            menu.AddItem(new GUIContent("Open in Search"), false, OpenStateInSearch);
        }

        public bool AddColumnHeaderContextMenuItems(GenericMenu menu)
        {
            var columnSetup = DependencyState.defaultColumns;

            menu.AddItem(new GUIContent("Open in Search"), false, OpenStateInSearch);
            menu.AddSeparator("");

            host.SelectDependencyColumns(menu, "Columns/");

            AddTableContextMenuItems(menu);

            menu.ShowAsContext();
            return true;
        }

        public bool OpenContextualMenu(Event evt, SearchItem item)
        {
            var menu = new GenericMenu();

            AddToItemContextualMenu(menu, item);

            menu.ShowAsContext();
            evt.Use();
            return true;
        }

        public void SetSelection(IEnumerable<SearchItem> items)
        {
            var firstItem = items.FirstOrDefault();
            if (firstItem == null)
                return;
            TrackSelection(firstItem);
        }

#if USE_SEARCH_EXTENSION_API
        public void OnItemExecuted(SearchItem item)
#else
        public void DoubleClick(SearchItem item)
#endif
        {
            // TODO Dep: not called 

            var obj = GetObject(item);
            if (!obj)
                return;
            host.PushViewerState(DependencyBuiltinStates.ObjectDependencies(obj, host.GetConfig()));
        }

        public void UpdateColumnSettings(int columnIndex, MultiColumnHeaderState.Column columnSettings)
        {
            var searchColumn = state.tableConfig.columns[columnIndex];
            searchColumn.width = columnSettings.width;
            searchColumn.content = columnSettings.headerContent;
            searchColumn.options &= ~SearchColumnFlags.TextAligmentMask;
            switch (columnSettings.headerTextAlignment)
            {
                case TextAlignment.Left: searchColumn.options |= SearchColumnFlags.TextAlignmentLeft; break;
                case TextAlignment.Center: searchColumn.options |= SearchColumnFlags.TextAlignmentCenter; break;
                case TextAlignment.Right: searchColumn.options |= SearchColumnFlags.TextAlignmentRight; break;
            }
        }

        public void AddTableContextMenuItems(GenericMenu menu)
        {
            var visibleColumnsLength = table.multiColumnHeader.state.visibleColumns.Length;
            for (int i = 0; i < visibleColumnsLength; i++)
            {
                var columnName = table.multiColumnHeader.state.columns[i].headerContent.text;
                menu.AddItem(EditorGUIUtility.TrTextContent($"Edit/{columnName}"), false, EditColumn, i);
            }
        }

        protected void EditColumn(object userData)
        {
            int columnIndex = (int)userData;
            var column = table.multiColumnHeader.state.columns[columnIndex];

#if USE_SEARCH_EXTENSION_API
            SearchUtils.ShowColumnEditor(column, (_column) => UpdateColumnSettings(columnIndex, _column));
#else
            ColumnEditor.ShowWindow(column, (_column) => UpdateColumnSettings(columnIndex, _column));
#endif
        }

        public void FrameColumn(int columnIndex)
        {
            table?.FrameColumn(columnIndex);
        }
    }
#endif

}
