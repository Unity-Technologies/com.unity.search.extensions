#if !UNITY_2021_1
using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using System.Linq;

namespace UnityEditor.Search
{
	class DependencyTableView : ITableView
	{
		readonly HashSet<SearchItem> m_Items;

		public readonly DependencyState state;
		public PropertyTable table;

		public SearchContext context => state.context;
		public IDependencyViewHost host { get; private set; }
		public bool empty => m_Items == null ? true : m_Items.Count == 0;

		public DependencyTableView(DependencyState state, IDependencyViewHost host)
		{
			this.host = host;
			this.state = state;
			m_Items = new HashSet<SearchItem>();
			Reload();
		}

		public void OnGUI(Rect rect)
		{
			table?.OnGUI(rect);
		}

		public void Reload()
		{
			m_Items.Clear();
			SearchService.Request(state.context, (c, items) => m_Items.UnionWith(items), _ => BuildTable());
		}

		public void AddColumn(Vector2 mousePosition, int activeColumnIndex)
		{
			#if USE_SEARCH_MODULE
			var columns = SearchColumn.Enumerate(context, GetElements());
			Utils.CallDelayed(() => ColumnSelector.AddColumns(AddColumns, columns, mousePosition, activeColumnIndex));
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

				table?.FrameColumn(insertColumnAt - 1);
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

		public bool IsReadOnly()
		{
			return false;
		}

		public void AddColumnHeaderContextMenuItems(GenericMenu menu, SearchColumn sourceColumn)
		{
		}

		public bool AddColumnHeaderContextMenuItems(GenericMenu menu)
		{
			var columnSetup = DependencyState.defaultColumns;

			#if !UNITY_2021
			menu.AddItem(new GUIContent("Open in Search"), false, OpenStateInSearch);
			menu.AddSeparator("");
			#endif

			host.SelectDependencyColumns(menu, "Columns/");

			var visibleColumnsLength = table.multiColumnHeader.state.visibleColumns.Length;
			for (int i = 0; i < visibleColumnsLength; i++)
			{
				var columnName = table.multiColumnHeader.state.columns[i].headerContent.text;
				menu.AddItem(EditorGUIUtility.TrTextContent($"Edit/{ columnName }"), false, EditColumn, i);
			}

			menu.ShowAsContext();
			return true;
		}

		private void EditColumn(object userData)
		{
			int columnIndex = (int)userData;
			var column = table.multiColumnHeader.state.columns[columnIndex];

			ColumnEditor.ShowWindow(column, (_column) => UpdateColumnSettings(columnIndex, _column));
		}

		public bool OpenContextualMenu(Event evt, SearchItem item)
		{
			var menu = new GenericMenu();

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

			var currentSelection = new[] { item };
			foreach (var action in item.provider.actions.Where(a => a.enabled(currentSelection)))
			{
				if (action.id == "select" || action.id == "copy")
					continue;
				itemName = !string.IsNullOrWhiteSpace(action.content.text) ? action.content.text : action.content.tooltip;
				menu.AddItem(new GUIContent($"Search/{itemName}"), false, () => ExecuteAction(action, currentSelection));
			}

			menu.ShowAsContext();
			evt.Use();
			return true;
		}

		bool TryGetGuid(in SearchItem item, out string guid)
        {
			guid = null;
            var obj = item.ToObject();
            if (!obj)
                return false;
            var gid = GlobalObjectId.GetGlobalObjectIdSlow(obj);
            guid = gid.assetGUID.ToString();
			return true;
        }

        void CopyAbsolutePath(in SearchItem item)
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

        void SelectObject(in SearchItem item)
        {
            var obj = item.ToObject();
			if (obj)
				Selection.activeObject = obj;
        }

        public void ExecuteAction(SearchAction action, SearchItem[] items)
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

		public void SetSelection(IEnumerable<SearchItem> items)
		{
			var firstItem = items.FirstOrDefault();
			if (firstItem == null)
				return;
			var obj = GetObject(firstItem);
			if (!obj)
				return;
			EditorGUIUtility.PingObject(obj);
		}

		public void DoubleClick(SearchItem item)
		{
			var obj = GetObject(item);
			if (!obj)
				return;
			host.PushViewerState(DependencyBuiltinStates.ObjectDependencies(obj, host.showSceneRefs));
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

			SearchColumnSettings.Save(searchColumn);
		}

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

		private void BuildTable()
		{
			table = new PropertyTable(state.guid, this);
			ResizeColumns();
			host.Repaint();
		}

        static string GetAssetPath(in SearchItem item)
        {
            if (item.provider.type == Providers.AssetProvider.type)
                return Providers.AssetProvider.GetAssetPath(item);
            if (item.provider.type == "dep")
                return AssetDatabase.GUIDToAssetPath(item.id);
            return null;
        }

        UnityEngine.Object GetObject(in SearchItem item)
		{
			UnityEngine.Object obj = null;
			var path = GetAssetPath(item);
			if (!string.IsNullOrEmpty(path))
				obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
			if (!obj)
				obj = item.ToObject();
			return obj;
		}

		#if !UNITY_2021
		private void OpenStateInSearch()
		{
			var searchViewState = new SearchViewState(state.context) { tableConfig = state.tableConfig.Clone(), itemSize = 1 };
			SearchService.ShowWindow(searchViewState);
		}
		#endif

		// ITableView
		public IEnumerable<SearchItem> GetRows() => throw new NotImplementedException();
		public SearchTable GetSearchTable() => throw new NotImplementedException();

        public void ResizeColumns()
        {
			var columns = table.multiColumnHeader.state.columns;
			foreach (var c in columns)
				c.autoResize = false;
			if (columns.Length == 0)
				return;
			columns[Math.Min(columns.Length-1, 1)].autoResize = true;
            table.multiColumnHeader.ResizeToFit();
		}
    }
}
#endif
