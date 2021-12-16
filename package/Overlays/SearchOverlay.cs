#if UNITY_2021_2_OR_NEWER
using UnityEngine;
using UnityEditor.Overlays;
using UnityEngine.UIElements;
using UnityEngine.Search;

namespace UnityEditor.Search
{
    abstract class SearchOverlay : ExtendedOverlay
    {
        public abstract string searchText { get; }
        public virtual SearchViewFlags searchViewFlags => SearchViewFlags.GridView;
        public override VisualElement CreateContainerContent() => new SearchView(searchText ?? string.Empty, searchViewFlags);
    }

    [Icon("Prefab Icon"), Overlay(typeof(SceneView), "Prefabs (Search)")]
    class PrefabsOverlay : SearchOverlay
    {
        public override string searchText => "p: t:prefab";
    }

    [Icon("GameObject Icon"), Overlay(typeof(SceneView), "Scene Objects (Search)")]
    class SceneObjectsOverlay : SearchOverlay
    {
        public override string searchText => "h: is:object";
        public override SearchViewFlags searchViewFlags => SearchViewFlags.ListView;
    }
}
#endif
