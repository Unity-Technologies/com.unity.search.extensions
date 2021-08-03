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
			menu.AddItem(new GUIContent("Open in Search"), false, OpenStateInSearch);
		}

		public bool OpenContextualMenu(Event evt, SearchItem item)
		{
			var menu = new GenericMenu();
			var currentSelection = new[] { item };
			foreach (var action in item.provider.actions.Where(a => a.enabled(currentSelection)))
			{
				var itemName = !string.IsNullOrWhiteSpace(action.content.text) ? action.content.text : action.content.tooltip;
				menu.AddItem(new GUIContent(itemName, action.content.image), false, () => ExecuteAction(action, currentSelection));
			}

			menu.ShowAsContext();
			evt.Use();
			return true;
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
			host.PushViewerState(DependencyBuiltinStates.ObjectDependencies(obj));
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

		UnityEngine.Object GetObject(in SearchItem item)
		{
			UnityEngine.Object obj = null;
			var path = SearchUtils.GetAssetPath(item);
			if (!string.IsNullOrEmpty(path))
				obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
			if (!obj)
				obj = item.ToObject();
			return obj;
		}

		private void OpenStateInSearch()
		{
			var searchViewState = new SearchViewState(state.context) { tableConfig = state.tableConfig.Clone() };
			SearchService.ShowWindow(searchViewState);
		}

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
