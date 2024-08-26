#if UNITY_2023_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.Search
{
    // Implement most of the ISearchView without bounding a UI View
    public class SearchViewModelEx : ISearchView
    {
        const int k_ResetSelectionIndex = -1;

        private GroupedSearchList m_FilteredItems;
        private SearchSelection m_SearchItemSelection;
        private int m_DelayedCurrentSelection = k_ResetSelectionIndex;
        private List<int> m_Selection;
        private SearchViewState m_ViewState;

        #region ISearchView Properties
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

        public SearchViewState state
        {
            get { return m_ViewState; }
            set
            {
                m_ViewState = value;
                multiselect = m_ViewState.context?.options.HasAny(SearchFlags.Multiselect) ?? false;
                SetSelection(Array.Empty<int>());
            }
        }

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

                // By default no refresh of content. Is it ok to assume this?
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
        public Action<GenericMenu, SearchItem> addToItemContextualMenu;
        public Action<SearchAction, SearchItem[], bool> executeAction;

        public SearchViewModelEx(SearchViewState state)
        {
            m_ViewState = state;
            m_FilteredItems = new GroupedSearchList(context);
            m_FilteredItems.currentGroup = viewState.group;
            m_Selection = new();
            m_SearchItemSelection = null;
        }

        #region ISearchView Methods
        public virtual void AddSelection(params int[] selection)
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

        public virtual void Dispose()
        {
            // Nothing to do;
        }

        public virtual void ExecuteAction(SearchAction action, SearchItem[] items, bool endSearch = false)
        {
            if (executeAction != null)
            {
                executeAction(action, items, endSearch);
                return;
            }

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

        public virtual void ExecuteSelection()
        {
            ExecuteAction(GetDefaultAction(selection, selection), selection.ToArray(), endSearch: false);
        }

        public virtual void Focus()
        {
            // Nothing by default: not tied to UI
        }

        public virtual void FocusSearch()
        {
            // Nothing by default: not tied to UI
        }

        public virtual bool IsPicker()
        {
            return false;
        }

        public virtual void Refresh(RefreshFlags reason = RefreshFlags.Default)
        {
            throw new NotImplementedException();
        }

        public virtual void Repaint()
        {
            // Nothing by default: not tied to UI => should be MarkDirtyRepaint
        }

        public virtual void SelectSearch()
        {
            // Nothing by default: not tied to UI => should be same thing as Focus.
        }

        public virtual void SetSearchText(string searchText, TextCursorPlacement moveCursor = TextCursorPlacement.MoveLineEnd)
        {
            // Nothing by default: not tied to UI => should be same thing as Focus.
        }

        public virtual void SetSelection(params int[] selection)
        {
            SetSelection(true, selection);
        }

        public void ShowItemContextualMenu(SearchItem item, Rect contextualActionPosition)
        {
            var menu = new GenericMenu();

            if (addToItemContextualMenu != null)
                addToItemContextualMenu?.Invoke(menu, item);
            else
                AddToItemContextualMenu(menu, item);

            if (contextualActionPosition == default)
                menu.ShowAsContext();
            else
                menu.DropDown(contextualActionPosition);
        }

        public virtual void AddToItemContextualMenu(GenericMenu menu, SearchItem item)
        {
            var shortcutIndex = 0;
            var useSelection = context?.selection?.Any(e => string.Equals(e.id, item.id, StringComparison.OrdinalIgnoreCase)) ?? false;
            var currentSelection = useSelection ? context.selection : new SearchSelection(new[] { item });
            foreach (var action in item.provider.actions.Where(a => a.enabled?.Invoke(currentSelection) ?? true))
            {
                var itemName = !string.IsNullOrWhiteSpace(action.content.text) ? action.content.text : action.content.tooltip;
                if (shortcutIndex == 0)
                    itemName += " _enter";
                else if (shortcutIndex == 1)
                    itemName += " _&enter";

                menu.AddItem(new GUIContent(itemName, action.content.image), false, () => ExecuteAction(action, currentSelection.ToArray()));
                ++shortcutIndex;
            }

            menu.AddSeparator("");
            if (SearchSettings.searchItemFavorites.Contains(item.id))
                menu.AddItem(new GUIContent("Remove from Favorites"), false, () => SearchSettings.RemoveItemFavorite(item));
            else
                menu.AddItem(new GUIContent("Add to Favorites"), false, () => SearchSettings.AddItemFavorite(item));
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
        public virtual void Close()
        {
            throw new NotSupportedException();
        }

        public virtual void SetColumns(IEnumerable<SearchColumn> columns)
        {
            throw new NotSupportedException();
        }

        void ISearchView.SetupColumns(IList<SearchField> fields)
        {
            throw new NotSupportedException();
        }

        public virtual void SetSearchText(string searchText, TextCursorPlacement moveCursor, int cursorInsertPosition)
        {
            throw new NotSupportedException("Cursor cannot be set for this control.");
        }
        #endregion

        #region ISearchView Implementation Taken mostly from internal SearchView.
        protected bool IsItemValid(int index)
        {
            if (index < 0 || index >= m_FilteredItems.Count)
                return false;
            return true;
        }

        protected void SetSelection(bool trackSelection, int[] selection, bool forceChange = false)
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

        protected void TrackSelection(int currentSelection)
        {
            if (trackingCallback == null)
                return;

            m_DelayedCurrentSelection = currentSelection;
            EditorApplication.delayCall -= DelayTrackSelection;
            EditorApplication.delayCall += DelayTrackSelection;
        }

        protected void DelayTrackSelection()
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