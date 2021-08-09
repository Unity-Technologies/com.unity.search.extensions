#if USE_SEARCH_TABLE
namespace UnityEditor.Search
{
	interface IDependencyViewHost
	{
		bool showSceneRefs { get; }
		void Repaint();
		void PushViewerState(DependencyViewerState state);
		void ToggleColumn(in DependencyState.Columns dc);
        void SelectDependencyColumns(GenericMenu menu, in string prefix);
    }
}
#endif
