#if UNITY_2021_2_OR_NEWER
using UnityEngine;
using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace UnityEditor.Search
{
    [Icon("SearchOverlay")]
    [Overlay(typeof(SceneView), "Zearch", defaultLayout = Layout.Panel)]
    class SearchOverlay : ExtendedOverlay
    {
        public override VisualElement CreateContainerContent()
        {
            return new SearchView("p: t:prefab");
        }
    }
}
#endif
