using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Search;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

namespace UnityEditor.Search
{
    public class SearchView : VisualElement, ISearchView
    {
        // Search
        private IResultView m_ResultView;
        private readonly SortedSearchList m_Results;
        private readonly List<int> m_Selection = new List<int>();

        // UITK
        private readonly IMGUIContainer m_ResultViewContainer;

        public float itemSize { get; private set; }
        public SearchViewState viewState { get; private set; }
        public Rect position { get; set; }
        public bool multiselect { get; set; }
        public ISearchList results => m_Results;
        public SearchContext context => viewState.context;

        DisplayMode ISearchView.displayMode => GetDisplayMode();
        float ISearchView.itemIconSize { get => itemSize; set => itemSize = value; }
        SearchSelection ISearchView.selection => new SearchSelection(m_Selection, m_Results);
        Action<SearchItem, bool> ISearchView.selectCallback => null;
        Func<SearchItem, bool> ISearchView.filterCallback => null;
        Action<SearchItem> ISearchView.trackingCallback => null;

        public SearchView(SearchViewState viewState)
        {
            this.viewState = viewState;

            itemSize = GetDefaultItemSize();
            multiselect = viewState.context?.options.HasAny(SearchFlags.Multiselect) ?? false;
            m_Results = new SortedSearchList(context);
            m_ResultView = CreateView();

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
            if (itemSize <= 32f && !(m_ResultView is ListView))
                return new ListView(this);
            else if (!(m_ResultView is GridView))
                return new GridView(this);
            return m_ResultView;
        }

        private void OnIncomingItems(SearchContext context, IEnumerable<SearchItem> items)
        {
            m_Results.AddItems(items);
        }

        private void OnQueryRequestFinished(SearchContext context)
        {
            Debug.Log($"{context.searchQuery} finished");
        }

        private void DrawSearchResults()
        {
            position = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            m_ResultView.Draw(position, m_Selection);
        }

        void ISearchView.SetSelection(params int[] selection)
        {
            m_Selection.Clear();
            m_Selection.AddRange(selection);
        }

        void ISearchView.SetSearchText(string searchText, TextCursorPlacement moveCursor) => throw new NotImplementedException();
        void ISearchView.SetSearchText(string searchText, TextCursorPlacement moveCursor, int cursorInsertPosition) => throw new NotImplementedException();
        void ISearchView.ExecuteAction(SearchAction action, SearchItem[] items, bool endSearch) => throw new NotImplementedException();
        void ISearchView.ExecuteSelection() => throw new NotImplementedException();
        void ISearchView.ShowItemContextualMenu(SearchItem item, Rect contextualActionPosition) => throw new NotImplementedException();
        void ISearchView.SelectSearch() => throw new NotImplementedException();

        void ISearchView.AddSelection(params int[] selection) => m_Selection.AddRange(selection);
        void ISearchView.FocusSearch() => Focus();
        void ISearchView.Repaint() => MarkDirtyRepaint();
        void ISearchView.Close() => throw new NotSupportedException();

        protected override void ExecuteDefaultAction(EventBase evt)
        {
            base.ExecuteDefaultAction(evt);
        }

        public override bool ContainsPoint(Vector2 localPoint)
        {
            Debug.Log("ContainsPoint");
            return base.ContainsPoint(localPoint);
        }

        protected override void ExecuteDefaultActionAtTarget(EventBase evt)
        {
            Debug.Log($"ExecuteDefaultActionAtTarget({evt.GetType().Name})");
            base.ExecuteDefaultActionAtTarget(evt);
        }

        public override bool Overlaps(Rect rectangle) => throw new NotImplementedException();
        protected override Vector2 DoMeasure(float desiredWidth, MeasureMode widthMode, float desiredHeight, MeasureMode heightMode) => throw new NotImplementedException();

        public override void Blur() => throw new NotImplementedException();
        public ValueAnimation<float> Start(float from, float to, int durationMs, Action<VisualElement, float> onValueChanged) => throw new NotImplementedException();
        public ValueAnimation<Rect> Start(Rect from, Rect to, int durationMs, Action<VisualElement, Rect> onValueChanged) => throw new NotImplementedException();
        public ValueAnimation<Color> Start(Color from, Color to, int durationMs, Action<VisualElement, Color> onValueChanged) => throw new NotImplementedException();
        public ValueAnimation<Vector3> Start(Vector3 from, Vector3 to, int durationMs, Action<VisualElement, Vector3> onValueChanged) => throw new NotImplementedException();
        public ValueAnimation<Vector2> Start(Vector2 from, Vector2 to, int durationMs, Action<VisualElement, Vector2> onValueChanged) => throw new NotImplementedException();
        public ValueAnimation<Quaternion> Start(Quaternion from, Quaternion to, int durationMs, Action<VisualElement, Quaternion> onValueChanged) => throw new NotImplementedException();
        public ValueAnimation<StyleValues> Start(StyleValues from, StyleValues to, int durationMs) => throw new NotImplementedException();
        public ValueAnimation<StyleValues> Start(StyleValues to, int durationMs) => throw new NotImplementedException();
        public ValueAnimation<float> Start(Func<VisualElement, float> fromValueGetter, float to, int durationMs, Action<VisualElement, float> onValueChanged) => throw new NotImplementedException();
        public ValueAnimation<Rect> Start(Func<VisualElement, Rect> fromValueGetter, Rect to, int durationMs, Action<VisualElement, Rect> onValueChanged) => throw new NotImplementedException();
        public ValueAnimation<Color> Start(Func<VisualElement, Color> fromValueGetter, Color to, int durationMs, Action<VisualElement, Color> onValueChanged) => throw new NotImplementedException();
        public ValueAnimation<Vector3> Start(Func<VisualElement, Vector3> fromValueGetter, Vector3 to, int durationMs, Action<VisualElement, Vector3> onValueChanged) => throw new NotImplementedException();
        public ValueAnimation<Vector2> Start(Func<VisualElement, Vector2> fromValueGetter, Vector2 to, int durationMs, Action<VisualElement, Vector2> onValueChanged) => throw new NotImplementedException();
        public ValueAnimation<Quaternion> Start(Func<VisualElement, Quaternion> fromValueGetter, Quaternion to, int durationMs, Action<VisualElement, Quaternion> onValueChanged) => throw new NotImplementedException();
        public ValueAnimation<Rect> Layout(Rect to, int durationMs) => throw new NotImplementedException();
        public ValueAnimation<Vector2> TopLeft(Vector2 to, int durationMs) => throw new NotImplementedException();
        public ValueAnimation<Vector2> Size(Vector2 to, int durationMs) => throw new NotImplementedException();
        public ValueAnimation<float> Scale(float to, int duration) => throw new NotImplementedException();
        public ValueAnimation<Vector3> Position(Vector3 to, int duration) => throw new NotImplementedException();
        public ValueAnimation<Quaternion> Rotation(Quaternion to, int duration) => throw new NotImplementedException();
        public IVisualElementScheduledItem Execute(Action<TimerState> timerUpdateEvent) => throw new NotImplementedException();
        public IVisualElementScheduledItem Execute(Action updateEvent) => throw new NotImplementedException();
    }
}
