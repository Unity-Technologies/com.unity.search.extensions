using System.Runtime.Remoting.Contexts;
using UnityEditor;
using UnityEditor.Search;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Search;
using UnityEngine.UIElements;

public class LightExplorerWindow : EditorWindow
{
    [MenuItem("Window/Rendering/Light Explorer (Search)")]
    public static void Open()
    {
        GetWindow<LightExplorerWindow>();
    }

    private const string k_SerializedPropertyProvider = "SerializedProperty";
    [SerializeField]  private SearchViewState m_SearchViewState;
    private ToolbarSearchField m_SearchField;

    private const string m_LightBaseQuery = "t=Light";
    private SearchTable m_LightTableConfig;

    private const string m_ProbeBaseQuery = "t=ReflectionProbe";
    private SearchTable m_ProbeTableConfig;

    private string m_BaseQuery;

    //  Note: SearchView is NOT public. So it needs to be developed from within trunk and Not from a package.
    private SearchView m_SearchView;

    private void OnEnable()
    {
        SearchMonitor.objectChanged += OnObjectChanged;
        SearchMonitor.sceneChanged += AsyncRefresh;
    }

    private void OnDisable()
    {
        SearchMonitor.objectChanged -= OnObjectChanged;
        SearchMonitor.sceneChanged -= AsyncRefresh;
        m_SearchView?.Dispose();
        m_SearchView = null;
    }

    private void CreateGUI()
    {
        VisualElement body = rootVisualElement;

        body.style.flexGrow = 1.0f;

        SearchElement.AppendStyleSheets(body);

        // 2 buttons to switch
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
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

        // Search Field
        m_SearchField = new ToolbarSearchField();
        body.Add(m_SearchField);
        m_SearchField.RegisterCallback<ChangeEvent<string>>(OnQueryChanged);

        m_LightTableConfig = CreateLightTableConfig();
        m_ProbeTableConfig = CreateProbeTableConfig();

        // Search view
        var context = SearchService.CreateContext(new[] { "scene" }, "");
        m_SearchViewState = new SearchViewState(context);
        m_SearchViewState.title = "Light Explorer";
        m_SearchViewState.flags = SearchViewFlags.DisableNoResultTips
            | SearchViewFlags.TableView
            | SearchViewFlags.DisableBuilderModeToggle
            | SearchViewFlags.DisableQueryHelpers;
        m_SearchViewState.itemSize = (float)DisplayMode.Table;
        m_SearchViewState.queryBuilderEnabled = true;
        m_SearchViewState.ignoreSaveSearches = true;
        m_SearchViewState.hideTabs = true;
        m_SearchViewState.itemSize = (float)UnityEditor.Search.DisplayMode.Table;
        m_SearchViewState.flags |= SearchViewFlags.DisableQueryHelpers;

        m_SearchView = new SearchView(m_SearchViewState, GetInstanceID());
        m_SearchView.AddToClassList("result-view");

        var groupBar = new SearchGroupBar("", m_SearchView);
        body.Add(groupBar);
        body.Add(m_SearchView);

        FilterLights();
    }

    public void FilterLights()
    {
        m_BaseQuery = m_LightBaseQuery;
        m_SearchViewState.tableConfig = m_LightTableConfig;
        // Note: This is necessary to force a refresh of the columns.
        m_SearchView.itemSize = (float)DisplayMode.Table;
        SetQuery("");
    }

    public void FilterReflectionProbes()
    {
        m_BaseQuery = m_ProbeBaseQuery;
        m_SearchViewState.tableConfig = m_ProbeTableConfig;
        // Note: This is necessary to force a refresh of the columns.
        m_SearchView.itemSize = (float)DisplayMode.Table;
        SetQuery("");
    }

    public void OnQueryChanged(ChangeEvent<string> evt)
    {
        // Refresh view. happens search text to base query
        SetQuery(evt.newValue);
    }

    public void SetQuery(string query)
    {
        var newQuery = $"{m_BaseQuery} {query}";
        m_SearchView.context.searchText = newQuery;
        Refresh();
    }

    public void Refresh()
    {
        m_SearchView.Refresh();
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

    static SearchColumn CreateColumn(string path, string selector, string displayName, string provider, int width = 100)
    {
        return new SearchColumn(path, selector, provider, new GUIContent(displayName)) { width = width };
    }
}
