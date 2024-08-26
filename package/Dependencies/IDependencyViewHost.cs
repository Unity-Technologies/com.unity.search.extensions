using System.Collections.Generic;

namespace UnityEditor.Search
{
    interface IDependencyViewHost
    {
        DependencyViewerConfig GetConfig();
        void Repaint();
        void PushViewerState(IEnumerable<string> idsOfInterest);
        void PushViewerState(DependencyViewerState state);
        void ToggleColumn(in DependencyState.Columns dc);
        void SelectDependencyColumns(GenericMenu menu, in string prefix);
    }
}
