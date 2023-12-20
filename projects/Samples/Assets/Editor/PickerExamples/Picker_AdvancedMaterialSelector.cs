using System;
using System.Linq;
using UnityEditor.Search;
using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.Search;

static class Picker_AdvancedMaterialSelector
{
    const string MaterialSelectorId = "material_selector";    

    // Callback that decide if the current ObjectSelector context can be applied to this special Picker
    [AdvancedObjectSelectorValidator(MaterialSelectorId)]
    static bool CanOpenSelector(ObjectSelectorSearchContext context)
    {
        // This selector only works for assets.
        if ((context.visibleObjects & VisibleObjects.Assets) == 0)
            return false;

        // This selector only supports materials and their derived types.
        if (!OnlyMaterialTypes(context))
            return false;

        return true;
    }

    static bool OnlyMaterialTypes(ObjectSelectorSearchContext context)
    {
        var requiredTypes = context.requiredTypes.Zip(context.requiredTypeNames, (type, name) => new Tuple<Type, string>(type, name));
        return requiredTypes.All(typeAndName =>
        {
            return typeAndName.Item1 != null && typeof(Material).IsAssignableFrom(typeAndName.Item1) ||
                typeAndName.Item2.Contains("material", StringComparison.OrdinalIgnoreCase);
        });
    }

    [AdvancedObjectSelector(MaterialSelectorId, "Material", 1)]
    static void SelectObject(AdvancedObjectSelectorEventType evt, in AdvancedObjectSelectorParameters args)
    {
        // evt will allow you to do stuff when starting/ending a search session.
        // In our case we only want to decide how to open our picker:
        if (evt != AdvancedObjectSelectorEventType.OpenAndSearch)
            return;

        var selectContext = args.context;
        var selectHandler = args.selectorClosedHandler;
        var trackingHandler = args.trackingHandler;

        // This selector handles any kind of materials, but if a specific material type is passed
        // in the context, then only this type of material will be shown.
        var searchText = "t:material ref:{t:shader unlit}";
        var searchContext = SearchService.CreateContext("asset", searchText);
        var viewState = new SearchViewState(searchContext,
            SearchViewFlags.GridView |
            SearchViewFlags.OpenInBuilderMode |
            SearchViewFlags.DisableSavedSearchQuery)
        {
            windowTitle = new GUIContent("Material Selector with unlit shader"),
            title = "Material with unlit shader",
            selectHandler = (item, canceled) => selectHandler(item?.ToObject(), canceled),
            trackingHandler = (item) => trackingHandler(item?.ToObject()),
            position = SearchUtils.GetMainWindowCenteredPosition(new Vector2(600, 400))
        };
        SearchService.ShowPicker(viewState);
    }

}
