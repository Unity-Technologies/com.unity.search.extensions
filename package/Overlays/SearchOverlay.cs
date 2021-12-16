#if UNITY_2022_1_OR_NEWER
using UnityEngine;
using UnityEditor.Overlays;
using UnityEngine.UIElements;
using UnityEngine.Search;

namespace UnityEditor.Search
{
    public abstract class SearchOverlay : ExtendedOverlay
    {
        public abstract string searchText { get; }
        public virtual float itemSize => -1;
        public virtual SearchViewFlags searchViewFlags => SearchViewFlags.GridView;
        protected override VisualElement CreateContainerContent()
        {
             var view = new SearchView(searchText ?? string.Empty, searchViewFlags);
             if (itemSize != -1)
                view.itemSize = itemSize;
             return view;
        }
    }

    #if ENABLE_SEARCH_OVERLAY_EXAMPLES

    [Icon("Prefab Icon"), Overlay(typeof(SceneView), "Prefabs (Search)")]
    class PrefabsOverlay : SearchOverlay
    {
        public override string searchText => "p: t:prefab";
    }

    [Icon("Material Icon"), Overlay(typeof(SceneView), "Materials (Search)")]
    class MaterialsOverlay : SearchOverlay
    {
        public override float itemSize => 128f;
        public override string searchText => "p: t=Material";
        public override SearchViewFlags searchViewFlags => SearchViewFlags.GridView;
    }

    [Icon("GameObject Icon"), Overlay(typeof(SceneView), "Scene Objects (Search)")]
    class SceneObjectsOverlay : SearchOverlay
    {
        public override string searchText => "h: is:object";
        public override SearchViewFlags searchViewFlags => SearchViewFlags.ListView;
    }

    #endif
}
#endif