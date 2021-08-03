#if !UNITY_2021_1
namespace UnityEditor.Search
{
	interface IDependencyViewHost
	{
		void Repaint();
		void PushViewerState(DependencyViewerState state);
	}
}
#endif
