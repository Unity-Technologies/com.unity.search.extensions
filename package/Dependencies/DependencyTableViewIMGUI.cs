#pragma warning disable CS0618
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace UnityEditor.Search
{
    class DependencyTableViewIMGUI : BaseDependencyTableView
    {
        public PropertyTable table;
        public DependencyTableViewIMGUI(DependencyState state, IDependencyViewHost host)
            : base(state, host)
        {
            SetState(state);
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

        public override void AddColumn(Vector2 mousePosition, int activeColumnIndex)
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

        public override void AddColumns(IEnumerable<SearchColumn> newColumns, int insertColumnAt)
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
                PopulateTableData();
                FrameColumn(insertColumnAt - 1);
            }
        }

        public override void SetupColumns(IEnumerable<SearchItem> elements = null)
        {
            PopulateTableData();
        }

        public override void RemoveColumn(int removeColumnAt)
        {
            if (removeColumnAt == -1)
                return;

            var columns = new List<SearchColumn>(state.tableConfig.columns);
            columns.RemoveAt(removeColumnAt);
            state.tableConfig.columns = columns.ToArray();
            PopulateTableData();
        }

        public override void SwapColumns(int columnIndex, int swappedColumnIndex)
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

        protected override void PopulateTableData()
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
                content.text += $" ({items?.Count() ?? 0})";
                table.multiColumnHeader.state.columns[columnCountIndex].headerContent = content;
            }
            host.Repaint();
        }

        public override void AddColumnHeaderContextMenuItems(GenericMenu menu, SearchColumn sourceColumn)
        {
            menu.AddItem(new GUIContent("Open in Search"), false, OpenStateInSearch);
        }

        public override bool AddColumnHeaderContextMenuItems(GenericMenu menu)
        {
            var columnSetup = DependencyState.defaultColumns;

            menu.AddItem(new GUIContent("Open in Search"), false, OpenStateInSearch);
            menu.AddSeparator("");

            host.SelectDependencyColumns(menu, "Columns/");

            AddTableContextMenuItems(menu);

            menu.ShowAsContext();
            return true;
        }

        public override bool OpenContextualMenu(Event evt, SearchItem item)
        {
            var menu = new GenericMenu();

            AddToItemContextualMenu(menu, item);

            menu.ShowAsContext();
            evt.Use();
            return true;
        }

        public override void SetSelection(IEnumerable<SearchItem> items)
        {
            var firstItem = items.FirstOrDefault();
            if (firstItem == null)
                return;
            TrackSelection(firstItem);
        }

#if USE_SEARCH_EXTENSION_API
        public override void OnItemExecuted(SearchItem item)
#else
        public override void DoubleClick(SearchItem item)
#endif
        {
            var obj = GetObject(item);
            if (!obj)
                return;
            host.PushViewerState(DependencyBuiltinStates.ObjectDependencies(obj, host.GetConfig()));
        }

        public override void UpdateColumnSettings(int columnIndex, MultiColumnHeaderState.Column columnSettings)
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
}