#if !UNITY_7000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.Search
{
    abstract class BaseDependencyTableView : ITableView
    {
        public DependencyState state { get; private set; }

        public SearchContext context => state.context;
        public IDependencyViewHost host { get; private set; }
        public bool empty => GetElements().Any();
        public VisualElement tableView { get; protected set; }

        protected BaseDependencyTableView(DependencyState state, IDependencyViewHost host)
        {
            this.host = host;
        }

        #region UIBackendSpecific Overridables
        public virtual void OnGUI(Rect rect)
        {
        }

        public void SetState(DependencyState state)
        {
            this.state = state;
            Reload();
        }

        public virtual void ExploreItem(SearchItem item)
        {
            var obj = GetObject(item);
            if (!obj)
                return;

            var idsOfInterest = Dependency.EnumerateIdFromObjects(new[] { obj } );
            if (idsOfInterest.Any())
            {
                host.PushViewerState(idsOfInterest);
            }
        }

        protected virtual void PopulateTableData()
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
#if USE_SEARCH_EXTENSION_API
        public bool readOnly => false;
#else
        public bool IsReadOnly()
        {
            return false;
        }
#endif

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
        public virtual IEnumerable<SearchItem> GetElements()
        {
            throw new NotImplementedException();
        }

        public virtual void Reload()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<SearchItem> GetRows() => throw new NotSupportedException();
        public SearchTable GetSearchTable() => throw new NotSupportedException();
        public virtual void AddColumn(Vector2 mousePosition, int activeColumnIndex)
        {
            throw new NotImplementedException();
        }

        public virtual void AddColumns(IEnumerable<SearchColumn> descriptors, int activeColumnIndex)
        {
            throw new NotImplementedException();
        }

        public virtual void SetupColumns(IEnumerable<SearchItem> elements = null)
        {
            throw new NotImplementedException();
        }

        public virtual void RemoveColumn(int activeColumnIndex)
        {
            throw new NotImplementedException();
        }

        public virtual void SwapColumns(int columnIndex, int swappedColumnIndex)
        {
            throw new NotImplementedException();
        }

        public virtual void SetSelection(IEnumerable<SearchItem> items)
        {
            throw new NotImplementedException();
        }

        public virtual void OnItemExecuted(SearchItem item)
        {
            throw new NotImplementedException();
        }

        public virtual bool OpenContextualMenu(Event evt, SearchItem item)
        {
            throw new NotImplementedException();
        }

        public virtual void UpdateColumnSettings(int columnIndex, MultiColumnHeaderState.Column columnSettings)
        {
            throw new NotImplementedException();
        }

        public virtual bool AddColumnHeaderContextMenuItems(GenericMenu menu)
        {
            throw new NotImplementedException();
        }

        public virtual void AddColumnHeaderContextMenuItems(GenericMenu menu, SearchColumn sourceColumn)
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

        internal void OpenStateInSearch()
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
}
#endif