#if !UNITY_2021_1
namespace UnityEditor.Search
{
	interface IDependencyViewHost
	{
		void Repaint();
		void PushViewerState(DependencyViewerState state);
		void ToggleColumn(in DependencyState.DependencyColumns dc);
        void SelectDependencyColumns(GenericMenu menu, in string prefix);
    }
}
#endif
