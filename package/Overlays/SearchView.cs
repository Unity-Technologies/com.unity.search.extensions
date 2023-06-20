using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Search;
using UnityEngine.UIElements;

namespace UnityEditor.Search
{
    public class SearchView : VisualElement, ISearchView
    {
        // Search
        private float m_ItemSize;
        private IResultView m_ResultView;
        private readonly SortedSearchList m_Results;
        private readonly List<int> m_Selection = new List<int>();

        // UITK
        private readonly IMGUIContainer m_ResultViewContainer;

        public float itemSize { get => m_ItemSize; set => SetItemSize(value); }
        public SearchViewState viewState { get; private set; }
        public Rect position { get; set; }
        public bool multiselect { get; set; }
        public ISearchList results => m_Results;
        public SearchContext context => viewState.context;
        public SearchSelection selection => new SearchSelection(m_Selection, m_Results);

        DisplayMode ISearchView.displayMode => GetDisplayMode();
        float ISearchView.itemIconSize { get => itemSize; set => itemSize = value; }
        Action<SearchItem, bool> ISearchView.selectCallback => null;
        Func<SearchItem, bool> ISearchView.filterCallback => null;
        Action<SearchItem> ISearchView.trackingCallback => null;

        public SearchView(SearchViewState viewState)
        {
            this.viewState = viewState;
            
            context.searchView = this;
            multiselect = viewState.context?.options.HasAny(SearchFlags.Multiselect) ?? false;
            m_Results = new SortedSearchList(context);
            itemSize = GetDefaultItemSize();

            m_ResultViewContainer = new IMGUIContainer(DrawSearchResults);
            m_ResultViewContainer.style.flexGrow = 1f;
            Add(m_ResultViewContainer);

            Refresh();
        }

        public SearchView(SearchContext context) : this(new SearchViewState(context, SearchViewFlags.GridView)) {}
        public SearchView(SearchContext context, SearchViewFlags flags) : this(new SearchViewState(context, flags)) {}

        public SearchView(in string searchText)
            : this(searchText, SearchViewFlags.GridView)
        {
        }

        public SearchView(in string searchText, SearchViewFlags flags)
            : this(SearchService.CreateContext(searchText ?? string.Empty, SearchFlags.OpenGlobal), flags)
        {
        }

        public void Refresh(RefreshFlags reason = RefreshFlags.Default)
        {
            m_Results.Clear();
            SearchService.Request(context, OnIncomingItems, OnQueryRequestFinished, SearchFlags.FirstBatchAsync);
        }

        public override string ToString() => context.searchText;
        public void Dispose() => viewState.context?.Dispose();

        private void SetItemSize(float value)
        {
            m_ItemSize = value;
            m_ResultView = CreateView();
        }

        private DisplayMode GetDisplayMode()
        {
            if (itemSize <= 32f)
                return DisplayMode.List;
            return DisplayMode.Grid;
        }

        private float GetDefaultItemSize()
        {
            if (viewState.flags.HasAny(SearchViewFlags.CompactView))
                return 1f;

            if (viewState.flags.HasAny(SearchViewFlags.GridView))
                return 64f;

            return (float)DisplayMode.List;
        }

        private IResultView CreateView()
        {

#if UNITY_2023_1_OR_NEWER
            if (itemSize <= 32f && !(m_ResultView is SearchListView))
                return new SearchListView(this);
            else if (!(m_ResultView is SearchGridView))
                return new SearchGridView(this);
            return m_ResultView;
#else
            if (itemSize <= 32f && !(m_ResultView is ListView))
                return new ListView(this);
            else if (!(m_ResultView is GridView))
                return new GridView(this);
            return m_ResultView;
#endif
        }

        private void OnIncomingItems(SearchContext context, IEnumerable<SearchItem> items)
        {
            m_Results.AddItems(items);
        }

        private void OnQueryRequestFinished(SearchContext context)
        {
            MarkDirtyRepaint();
        }

        private void DrawSearchResults()
        {
            position = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            #if !(UNITY_2023_1_OR_NEWER)
            m_ResultView.Draw(position, m_Selection);
            #endif
        }

        void ISearchView.SetSelection(params int[] selection)
        {
            m_Selection.Clear();
            m_Selection.AddRange(selection);
        }

        void ISearchView.ShowItemContextualMenu(SearchItem item, Rect contextualActionPosition)
        {
            var menu = new GenericMenu();

            var useSelection = context?.selection?.Any(e => string.Equals(e.id, item.id, StringComparison.OrdinalIgnoreCase)) ?? false;
            var currentSelection = useSelection ? context.selection : new SearchSelection(new[] { item });
            foreach (var action in item.provider.actions.Where(a => a.enabled?.Invoke(currentSelection) ?? true))
            {
                var itemName = !string.IsNullOrWhiteSpace(action.content.text) ? action.content.text : action.content.tooltip;
                menu.AddItem(new GUIContent(itemName, action.content.image), false, () => ExecuteAction(action, currentSelection.ToArray()));
            }

            if (menu.GetItemCount() > 0)
                menu.AddSeparator("");
            if (!SearchSettings.searchItemFavorites.Contains(item.id))
                menu.AddItem(new GUIContent("Add to Favorites"), false, () => SearchSettings.AddItemFavorite(item));
            else
                menu.AddItem(new GUIContent("Remove from Favorites"), false, () => SearchSettings.RemoveItemFavorite(item));

            menu.ShowAsContext();
        }

        void ISearchView.ExecuteSelection() => ExecuteAction(selection.First().provider.actions.First(a => a.id == "select"), selection.ToArray());
        void ISearchView.ExecuteAction(SearchAction action, SearchItem[] items, bool endSearch) => ExecuteAction(action, items);
        private void ExecuteAction(in SearchAction action, in SearchItem[] items)
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

        void ISearchView.SetSearchText(string searchText, TextCursorPlacement moveCursor)
        {
            context.searchText = searchText;
            Refresh(RefreshFlags.ItemsChanged);
        }

        void ISearchView.AddSelection(params int[] selection) => m_Selection.AddRange(selection);
        void ISearchView.FocusSearch() => Focus();
        void ISearchView.Repaint() => MarkDirtyRepaint();

        void ISearchView.Close() => throw new NotSupportedException();
        void ISearchView.SelectSearch() => throw new NotSupportedException();
        void ISearchView.SetSearchText(string searchText, TextCursorPlacement moveCursor, int cursorInsertPosition) => throw new NotSupportedException();

#if UNITY_2022_2_OR_NEWER
        bool ISearchView.IsPicker()
        {
            return false;
        }
#endif

#if UNITY_2022_2 || UNITY_2022_3
        int ISearchView.cursorIndex => 0;
#endif

#if USE_SEARCH_EXTENSION_API
        void ISearchView.SetColumns(IEnumerable<SearchColumn> columns) => throw new NotSupportedException();
#endif

#if UNITY_2023_1_OR_NEWER
        SearchViewState ISearchView.state => throw new NotImplementedException();

        string ISearchView.currentGroup { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        bool ISearchView.searchInProgress => throw new NotImplementedException();

        int ISearchView.totalCount => throw new NotImplementedException();

        bool ISearchView.syncSearch { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        SearchPreviewManager ISearchView.previewManager => throw new NotImplementedException();
        IEnumerable<IGroup> ISearchView.EnumerateGroups()
        {
            throw new NotImplementedException();
        }

        void ISearchView.SetupColumns(IList<SearchField> fields)
        {
            throw new NotImplementedException();
        }

        IEnumerable<SearchQueryError> ISearchView.GetAllVisibleErrors()
        {
            throw new NotImplementedException();
        }

        int ISearchView.GetViewId()
        {
            return GetHashCode();
        }
#endif
    }
}
