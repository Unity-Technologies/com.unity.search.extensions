using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.Search;
using UnityEngine.UIElements;

// Creating a custom provider allows us to resolve the query like we want and to tweak the query passed to the Scene provider.
class LightSearchProvider : SearchProvider
{
    static string providerId = "light";

    private const string m_LightBaseQuery = "t=Light";
    private const string m_ProbeBaseQuery = "t=ReflectionProbe";
    private const string k_SerializedPropertyProvider = "SerializedProperty";

    private SearchTable m_LightTableConfig;
    private SearchTable m_ProbeTableConfig;

    public enum LightType
    {
        Light,
        ReflectionProbe
    }

    public LightType lightType { get; set; }
    public string baseQuery => lightType == LightType.Light ? m_LightBaseQuery : m_ProbeBaseQuery;
    public SearchTable lightTableConfig => lightType == LightType.Light ? m_LightTableConfig : m_ProbeTableConfig;


    public LightSearchProvider()
        : base(providerId)
    {
        fetchItems = (context, items, provider) => SearchItems(context, provider);
        toObject = (item, type) => item.ToObject();
        fetchPropositions = FetchPropositions;
        lightType = LightType.Light;
        m_LightTableConfig = CreateLightTableConfig();
        m_ProbeTableConfig = CreateProbeTableConfig();
    }

    IEnumerable<SearchItem> SearchItems(SearchContext context, SearchProvider provider)
    {
        var query = $"{baseQuery} {context.searchQuery}";
        using var sceneContext = SearchService.CreateContext("scene", query);
        using var request = SearchService.Request(sceneContext);
        while (request.pending)
            yield return null;
        foreach (var item in request)
        {
            yield return item;
        }
    }

    static SearchTable CreateLightTableConfig()
    {
        return new SearchTable("lights", new SearchColumn[]
        {
            CreateColumn("GameObject/Enabled", "enabled", "Enabled", "GameObject/Enabled", 60),
            CreateColumn("Name", "Name", "Name", "Name", 180),
            CreateColumn("Light/m_Type", "#m_Type", "Type", k_SerializedPropertyProvider, 105),
            CreateColumn("Light/m_Lightmapping", "#m_Lightmapping", "Lightmapping", k_SerializedPropertyProvider, 115),
            CreateColumn("Light/m_Color", "#m_Color", "Color", k_SerializedPropertyProvider, 55),
            CreateColumn("Light/m_Range", "#m_Range", "Range", k_SerializedPropertyProvider, 70),
            CreateColumn("Light/m_Intensity", "#m_Intensity", "Intensity", k_SerializedPropertyProvider, 75),
            CreateColumn("Light/m_BounceIntensity", "#m_BounceIntensity", "Indirect Multiplier", k_SerializedPropertyProvider, 115),
            CreateColumn("Light/m_Shadows/m_Type", "#m_Shadows.m_Type", "Shadows", k_SerializedPropertyProvider, 120)
        });
    }

    static IEnumerable<SearchProposition> GetLightPropositions()
    {
        var blockType = typeof(Light);

        // Returns propositions fitting the columns: 
        yield return CreateProposition(blockType, "Type", "#Light.m_Type=<$enum:Directional,UnityEngine.LightType$>");
        yield return CreateProposition(blockType, "Light Mapping", "#Light.m_Lightmapping=<$enum:Baked,UnityEngine.LightmappingMode$>");
        yield return CreateProposition(blockType, "Intensity", "#m_Intensity>0");
        yield return CreateProposition(blockType, "Indirect Multiplier", "#m_BounceIntensity>1");
        yield return CreateProposition(blockType, "Shadows", "#Light.m_Shadows.m_Type=<$enum:Hard,UnityEngine.LightShadows$>");

    }

    static SearchTable CreateProbeTableConfig()
    {
        return new SearchTable("lights", new SearchColumn[]
        {
            CreateColumn("GameObject/Enabled", "enabled", "Enabled", "GameObject/Enabled", 60),
            CreateColumn("Name", "Name", "Name", "Name", 180),

            CreateColumn("ReflectionProbe/m_Mode", "#m_Mode", "Mode", k_SerializedPropertyProvider, 80),
            CreateColumn("ReflectionProbe/m_HDR", "#m_HDR", "HDR", k_SerializedPropertyProvider, 60),
            CreateColumn("ReflectionProbe/m_ShadowDistance", "#m_ShadowDistance", "ShadowDistance", k_SerializedPropertyProvider, 130),
            CreateColumn("ReflectionProbe/m_NearClip", "#m_NearClip", "Near Plane", k_SerializedPropertyProvider, 80),
            CreateColumn("ReflectionProbe/m_FarClip", "#m_FarClip", "Far Plane", k_SerializedPropertyProvider, 65),
        });
    }

    static IEnumerable<SearchProposition> GetProbePropositions()
    {
        var blockType = typeof(ReflectionProbe);

        // Returns propositions fitting the columns: 
        yield return CreateProposition(blockType, "Mode", "#ReflectionProbe.m_Mode=<$enum:Baked,UnityEngine.ReflectionProbeMode$>");
        yield return CreateProposition(blockType, "HDR", "#HDR=true");
        yield return CreateProposition(blockType, "Near Plane", "#m_NearClip>0.1");
        yield return CreateProposition(blockType, "Far Plane", "#m_FarClip>0.1");
    }

    static SearchProposition CreateProposition(Type blockType, string label, string replacement)
    {
        return new SearchProposition(
            category: null,
            label: label,
            replacement: replacement,
            moveCursor: TextCursorPlacement.MoveAutoComplete,
            icon: AssetPreview.GetMiniTypeThumbnailFromType(blockType),
            type: null,
            data: null,
            color: QueryColors.property
        );
    }

    static SearchColumn CreateColumn(string path, string selector, string displayName, string provider, int width = 100)
    {
        return new SearchColumn(path, selector, provider, new GUIContent(displayName)) { width = width };
    }

    IEnumerable<SearchProposition> FetchPropositions(SearchContext context, SearchPropositionOptions options)
    {
        // We can return propositions that make sense only for a Light object
        var props = lightType == LightType.Light ? GetLightPropositions() : GetProbePropositions();

        foreach (var searchProposition in props)
        {
            yield return searchProposition;
        }
    }
}

class LightExplorerWindow : EditorWindow
{
    [MenuItem("Window/Rendering/Light Explorer (Search)")]
    public static void Open()
    {
        GetWindow<LightExplorerWindow>();
    }

    [SerializeField] private UnityEditor.Search.SearchViewModelEx m_ViewModel;
    internal SearchViewState viewState => m_ViewModel.viewState;


    SearchFieldElement m_SearchField;
    SearchTableView m_SearchTableView;
    LightSearchProvider m_LightSearchProvider;

    private void OnEnable()
    {
        SearchMonitor.objectChanged += OnObjectChanged;
        SearchMonitor.sceneChanged += AsyncRefresh;
    }

    private void OnDisable()
    {
        SearchMonitor.objectChanged -= OnObjectChanged;
        SearchMonitor.sceneChanged -= AsyncRefresh;
    }

    private void CreateGUI()
    {
        m_LightSearchProvider = new LightSearchProvider();

        var body = rootVisualElement;

        body.style.flexGrow = 1.0f;

        SearchElement.AppendStyleSheets(body);

        // 2 buttons to switch between types of lights
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.height = 28;
            {
                var btn = new Button(FilterLights);
                btn.text = "Lights";
                row.Add(btn);
            }

            {
                var btn = new Button(FilterReflectionProbes);
                btn.text = "Probes";
                row.Add(btn);
            }

            {
                var btn = new Button(Refresh);
                btn.text = "Refresh";
                row.Add(btn);
            }
            body.Add(row);
        }

        // Search view Model
        var context = SearchService.CreateContext(m_LightSearchProvider, "");
        var searchViewState = new SearchViewState(context);
        searchViewState.title = "Light Explorer";
        searchViewState.flags = SearchViewFlags.DisableNoResultTips
            | SearchViewFlags.TableView
            | SearchViewFlags.DisableBuilderModeToggle
            | SearchViewFlags.DisableQueryHelpers;
        searchViewState.itemSize = (float)DisplayMode.Table;
        searchViewState.queryBuilderEnabled = true;
        searchViewState.ignoreSaveSearches = true;
        searchViewState.hideTabs = true;
        searchViewState.itemSize = (float)UnityEditor.Search.DisplayMode.Table;
        searchViewState.flags |= SearchViewFlags.DisableQueryHelpers;

        m_ViewModel = new UnityEditor.Search.SearchViewModelEx(searchViewState);
        m_ViewModel.queryChanged += OnQueryChanged;
        // TODO ViewModel: context.searchView will be obsolete.
        context.searchView = m_ViewModel;

        var searchToolbar = new VisualElement();
        searchToolbar.style.flexDirection = FlexDirection.Row;

        m_SearchField = new SearchFieldElement("SearchField", m_ViewModel, false);
        m_SearchField.style.height = 32;
        searchToolbar.Add(m_SearchField);

        m_SearchTableView = new SearchTableView(m_ViewModel);
        m_SearchTableView.style.flexGrow = 1;

        body.Add(searchToolbar);
        body.Add(m_SearchTableView);

        FilterLights();
    }

    public void FilterLights()
    {
        FilterItems(LightSearchProvider.LightType.Light);
    }

    public void FilterReflectionProbes()
    {
        FilterItems(LightSearchProvider.LightType.ReflectionProbe);
    }

    public void FilterItems(LightSearchProvider.LightType lightType)
    {
        m_LightSearchProvider.lightType = lightType;
        viewState.tableConfig = m_LightSearchProvider.lightTableConfig;
        m_SearchTableView.Refresh(RefreshFlags.DisplayModeChanged);

        SetQuery("");
    }

    public void OnQueryChanged(SearchViewModelEx viewModel, string searchText)
    {
        SetQuery(searchText);
    }

    public void SetQuery(string query)
    {
        m_SearchField.searchTextInput.SetValueWithoutNotify(query);
        m_ViewModel.context.searchText = query;
        Refresh();
    }

    public void Refresh()
    {
        m_ViewModel.RefreshItems(null, () => m_SearchTableView.Refresh(RefreshFlags.ItemsChanged));
    }

    private void AsyncRefresh()
    {
        EditorApplication.delayCall -= Refresh;
        EditorApplication.delayCall += Refresh;
    }

    private void OnObjectChanged(ref ObjectChangeEventStream stream)
    {
        for (int i = 0; i < stream.length; ++i)
        {
            var eventType = stream.GetEventType(i);
            switch (eventType)
            {
                case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                case ObjectChangeKind.ChangeGameObjectStructure:
                case ObjectChangeKind.ChangeGameObjectParent:
                case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                case ObjectChangeKind.UpdatePrefabInstances:
                case ObjectChangeKind.None:
                case ObjectChangeKind.CreateAssetObject:
                case ObjectChangeKind.DestroyAssetObject:
                case ObjectChangeKind.ChangeAssetObjectProperties:
                    break;

                case ObjectChangeKind.ChangeScene:
                case ObjectChangeKind.CreateGameObjectHierarchy:
                case ObjectChangeKind.DestroyGameObjectHierarchy:
                    AsyncRefresh();
                    break;
            }
        }
    }
}
