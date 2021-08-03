namespace UnityEditor.Search
{
	interface IDependencyViewHost
	{
		void Repaint();
		void PushViewerState(DependencyViewerState state);
	}
}
