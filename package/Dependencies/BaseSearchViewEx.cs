#if UNITY_2023_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.Search
{
    public class BaseSearchViewEx : ISearchView
    {
        const int k_ResetSelectionIndex = -1;

        private GroupedSearchList m_FilteredItems;
        private SearchSelection m_SearchItemSelection;
        private int m_DelayedCurrentSelection = k_ResetSelectionIndex;
        private List<int> m_Selection;
        private bool disposedValue;
        private SearchViewState m_ViewState;

        #region ISearchView
        public SearchSelection selection
        {
            get
            {
                if (m_SearchItemSelection == null)
                    m_SearchItemSelection = new SearchSelection(m_Selection, m_FilteredItems);
                return m_SearchItemSelection;
            }
        }

        public ISearchList results => m_FilteredItems;

        public SearchContext context => m_ViewState.context;

        public SearchViewState state => m_ViewState;

        public string currentGroup
        {
            get => m_FilteredItems.currentGroup;
            set
            {
                var prevGroup = currentGroup;
                viewState.groupChanged?.Invoke(context, value, currentGroup);

                var selectedItems = m_SearchItemSelection != null ? m_SearchItemSelection.ToArray() : Array.Empty<SearchItem>();
                var newSelectedIndices = new int[selectedItems.Length];

                viewState.group = value;
                m_FilteredItems.currentGroup = value;

                // TODO Dep : What to do?
                // RefreshContent(RefreshFlags.GroupChanged);

                for (var i = 0; i < selectedItems.Length; ++i)
                {
                    var selectedItem = selectedItems[i];
                    newSelectedIndices[i] = m_FilteredItems.IndexOf(selectedItem);
                }
                SetSelection(true, newSelectedIndices, true);
            }
        }
        public float itemIconSize { get; set; }

        public DisplayMode displayMode { get; set; }

        public bool multiselect { get; set; }

        public Rect position { get; set; }

        public bool searchInProgress => m_ViewState.context.searchInProgress;

        public Action<SearchItem, bool> selectCallback { get; set; }

        public Func<SearchItem, bool> filterCallback { get; set; }

        public Action<SearchItem> trackingCallback { get; set; }

        int ISearchView.totalCount => m_FilteredItems.TotalCount;

        bool ISearchView.syncSearch { get; set; }

        SearchPreviewManager ISearchView.previewManager => null;
        #endregion

        public SearchViewState viewState => m_ViewState;
        public int viewId { get; set; }

        public BaseSearchViewEx(SearchViewState state)
        {
            m_ViewState = state;

            context.searchView = context.searchView ?? this;
            multiselect = viewState.context?.options.HasAny(SearchFlags.Multiselect) ?? false;
            m_Selection = new();
            m_FilteredItems = new GroupedSearchList(context);
            m_FilteredItems.currentGroup = viewState.group;
        }

        #region ISearchView
        public void AddSelection(params int[] selection)
        {
            if (!multiselect && m_Selection.Count == 1)
                throw new Exception("Multi selection is not allowed.");

            foreach (var idx in selection)
            {
                if (!IsItemValid(idx))
                    continue;

                if (m_Selection.Contains(idx))
                {
                    m_Selection.Remove(idx);
                }
                else
                {
                    m_Selection.Add(idx);
                }
            }

            SetSelection(true, m_Selection.ToArray());
        }

        public void Dispose()
        {
            // Nothing to do;
        }

        public void ExecuteAction(SearchAction action, SearchItem[] items, bool endSearch = false)
        {
            var item = items.LastOrDefault();
            if (item == null)
                return;

            if (m_ViewState.selectHandler != null && items.Length > 0)
            {
                m_ViewState.selectHandler(items[0], false);
                m_ViewState.selectHandler = null;
                if (IsPicker())
                    endSearch = true;
            }
            else
            {
                if (action == null)
                    action = GetDefaultAction(selection, items);

                if (endSearch)
                    EditorApplication.delayCall -= DelayTrackSelection;

                if (action?.handler != null && items.Length == 1)
                    action.handler(item);
                else if (action?.execute != null)
                    action.execute(items);
                else
                    action?.handler?.Invoke(item);
            }
        }

        public void ExecuteSelection()
        {
            ExecuteAction(GetDefaultAction(selection, selection), selection.ToArray(), endSearch: false);
        }

        public void Focus()
        {
            // Nothing by default: not tied to UI
        }

        public void FocusSearch()
        {
            // Nothing by default: not tied to UI
        }

        public bool IsPicker()
        {
            return false;
        }

        public void Refresh(RefreshFlags reason = RefreshFlags.Default)
        {
            throw new NotImplementedException();
        }

        public void Repaint()
        {
            // Nothing by default: not tied to UI => should be MarkDirtyRepaint
        }

        public void SelectSearch()
        {
            // Nothing by default: not tied to UI => should be same thing as Focus.
        }


        public void SetSearchText(string searchText, TextCursorPlacement moveCursor = TextCursorPlacement.MoveLineEnd)
        {
            // TODO Dep: is the search done here or on the DepView side?
            throw new NotImplementedException();
        }

        public void SetSelection(params int[] selection)
        {
            SetSelection(true, selection);
        }

        public void ShowItemContextualMenu(SearchItem item, Rect contextualActionPosition)
        {
            // TODO Dep? 
        }

        IEnumerable<IGroup> ISearchView.EnumerateGroups()
        {
            return EnumerateGroups(!viewState.hideAllGroup);
        }

        IEnumerable<SearchQueryError> ISearchView.GetAllVisibleErrors()
        {
            var visibleProviders = ((ISearchView)this).EnumerateGroups().Select(g => g.id).ToArray();
            var defaultProvider = SearchService.GetDefaultProvider();
            return context.GetAllErrors().Where(e => visibleProviders.Contains(e.provider.type) || e.provider.type == defaultProvider.type);
        }

        int ISearchView.GetViewId()
        {
            return viewId;
        }
        #endregion

        #region BaseSearchView Unsupported
        public void Close()
        {
            throw new NotSupportedException();
        }

        public void SetColumns(IEnumerable<SearchColumn> columns)
        {
            throw new NotSupportedException();
        }

        void ISearchView.SetupColumns(IList<SearchField> fields)
        {
            throw new NotSupportedException();
        }

        public void SetSearchText(string searchText, TextCursorPlacement moveCursor, int cursorInsertPosition)
        {
            throw new NotSupportedException("Cursor cannot be set for this control.");
        }
        #endregion

        #region ISearchView Implementation Taken mostly from internal SearchView.
        private bool IsItemValid(int index)
        {
            if (index < 0 || index >= m_FilteredItems.Count)
                return false;
            return true;
        }

        private void SetSelection(bool trackSelection, int[] selection, bool forceChange = false)
        {
            if (!multiselect && selection.Length > 1)
                selection = new int[] { selection[selection.Length - 1] };

            var selectedIds = new List<int>();
            var lastIndexAdded = k_ResetSelectionIndex;

            m_Selection.Clear();
            m_SearchItemSelection = null;
            foreach (var idx in selection)
            {
                if (!IsItemValid(idx))
                    continue;

                selectedIds.Add(m_FilteredItems[idx].GetInstanceId());
                m_Selection.Add(idx);
                lastIndexAdded = idx;
            }

            if (lastIndexAdded != k_ResetSelectionIndex || forceChange)
            {
                m_SearchItemSelection = null;
                viewState.selectedIds = selectedIds.ToArray();
                if (trackSelection)
                    TrackSelection(lastIndexAdded);
            }
        }

        private void TrackSelection(int currentSelection)
        {
            if (trackingCallback == null)
                return;

            m_DelayedCurrentSelection = currentSelection;
            EditorApplication.delayCall -= DelayTrackSelection;
            EditorApplication.delayCall += DelayTrackSelection;
        }

        private void DelayTrackSelection()
        {
            if (m_FilteredItems.Count == 0)
                return;

            if (!IsItemValid(m_DelayedCurrentSelection))
                return;

            var selectedItem = m_FilteredItems[m_DelayedCurrentSelection];
            if (trackingCallback == null)
                selectedItem?.provider?.trackSelection?.Invoke(selectedItem, context);
            else
                trackingCallback(selectedItem);

            m_DelayedCurrentSelection = k_ResetSelectionIndex;
        }

        internal static SearchAction GetDefaultAction(SearchSelection selection, IEnumerable<SearchItem> items)
        {
            var provider = (items ?? selection).First().provider;
            return provider.actions.FirstOrDefault();
        }

        internal static SearchAction GetSecondaryAction(SearchSelection selection, IEnumerable<SearchItem> items)
        {
            var provider = (items ?? selection).First().provider;
            return provider.actions.Count > 1 ? provider.actions[1] : GetDefaultAction(selection, items);
        }

        internal IEnumerable<IGroup> EnumerateGroups(bool showAll)
        {
            var groups = m_FilteredItems.EnumerateGroups(showAll);
            if (showAll)
                groups = groups.Where(g => !string.Equals(g.id, "default", StringComparison.Ordinal));
            return groups;
        }

        internal IGroup GetGroupById(string groupId)
        {
            return m_FilteredItems.GetGroupById(groupId);
        }

        public int IndexOf(SearchItem item)
        {
            return m_FilteredItems.IndexOf(item);
        }

        public bool Add(SearchItem item)
        {
            if (m_FilteredItems.Contains(item))
                return false;
            m_FilteredItems.Add(item);
            return true;
        }
        #endregion
    }
}

#endif