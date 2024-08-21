#if UNITY_2023_1_OR_NEWER
using UnityEngine.UIElements;

namespace UnityEditor.Search
{
    // This is wrapper between a SearchView and the actual TableView.
    class DependencyTableViewUITk : BaseDependencyTableView
    {
        private SearchTableView m_TableView;   // Actual TableView
        private SearchViewModelEx m_SearchViewModel; // Bound to m_TableView
        public bool tableExists => m_SearchViewModel != null;

        public DependencyTableViewUITk(DependencyState state, IDependencyViewHost host)
            : base(state, host)
        {
            m_SearchViewModel = new SearchViewModelEx(state.viewState);
            m_SearchViewModel.addToItemContextualMenu = this.AddToItemContextualMenu;
            m_SearchViewModel.trackingCallback = TrackSelection;
            m_TableView = new SearchTableView(m_SearchViewModel);
            m_TableView.style.flexGrow = 1;
            tableView = m_TableView;

            var listView = m_TableView.Q<MultiColumnListView>();
            foreach (var c in listView.columns)
            {
                if (c.title == state.name)
                {
                    c.stretchable = true;
                    break;
                }
            }

            SetState(state);
        }
        #region TableView Overrides
        protected override void PopulateTableData()
        {
            m_SearchViewModel.state = state.viewState;
            m_SearchViewModel.results.Clear();
            m_SearchViewModel.results.AddItems(items);
            m_TableView.Refresh(RefreshFlags.ItemsChanged);
        }
        #endregion
    }
}
#endif