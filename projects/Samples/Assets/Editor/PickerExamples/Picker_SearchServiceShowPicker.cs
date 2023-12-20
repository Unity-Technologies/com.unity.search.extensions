using UnityEditor;
using UnityEditor.Search;
using UnityEngine.Search;
using UnityEngine.UIElements;

public class Picker_SearchServiceShowPicker : EditorWindow
{
    private void OnEnable()
    {
        var flags = SearchViewFlags.OpenInBuilderMode | SearchViewFlags.ListView | SearchViewFlags.DisableBuilderModeToggle;

        var searchContext = SearchService.CreateContext("asset", "t:material ref:{t:shader unlit}");
        var objecField = new ObjectField("Material");
        objecField.searchContext = searchContext;
        objecField.searchViewFlags = flags;
        rootVisualElement.Add(objecField);

        var nothingSelected = "No Material Selected";
        var label = new Label(nothingSelected);
        var button = new Button(() =>
        {
            var searchViewState = new SearchViewState(searchContext)
            {
                ignoreSaveSearches = true
            };
            SearchService.ShowPicker(searchContext, (item, canceled) =>
            {
                if (canceled)
                    label.text = nothingSelected;
                else
                    label.text = item.label;
            });
        });
        button.text = "Show Picker and select material";
        rootVisualElement.Add(button);
        rootVisualElement.Add(label);
    }

    [MenuItem("Window/Search/Select Some Material")]
    static private void OpenMyMaterialPicker()
    {
        GetWindow<Picker_SearchServiceShowPicker>();
    }
}
