using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.Search;
using UnityEngine.Search;

[UnityEditor.SearchService.ObjectSelectorEngine]
class DecalPicker : UnityEditor.SearchService.IObjectSelectorEngine
{
    #region DecalShaderListBlock
    [QueryListBlock("Decal", "Shader", "shader")]
    class ShaderDecalBlockList : QueryListBlock
    {
        public ShaderDecalBlockList(IQuerySource source, string id, string value, QueryListBlockAttribute attr)
            : base(source, id, value, attr)
        {
        }

        public override IEnumerable<SearchProposition> GetPropositions(SearchPropositionFlags flags = SearchPropositionFlags.None)
        {
            yield return new SearchProposition(category: null, "HDRP Decal", "Decal", icon: GetShaderIcon() as Texture2D);
            yield return new SearchProposition(category: null, "URP Decal", "DecalURP", icon: GetShaderIcon() as Texture2D);
        }
    }
    #endregion

    string UnityEditor.SearchService.ISearchEngineBase.name => "Decal Material Selector";
    internal static Texture GetShaderIcon() => EditorGUIUtility.Load("Shader Icon") as Texture;
    internal static Texture GetMaterialIcon() => EditorGUIUtility.Load("Material Icon") as Texture;

    void UnityEditor.SearchService.ISearchEngineBase.BeginSearch(UnityEditor.SearchService.ISearchContext context, string query) {}
    void UnityEditor.SearchService.ISearchEngineBase.EndSearch(UnityEditor.SearchService.ISearchContext context) {}
    void UnityEditor.SearchService.ISearchEngineBase.BeginSession(UnityEditor.SearchService.ISearchContext context) { }
    void UnityEditor.SearchService.ISearchEngineBase.EndSession(UnityEditor.SearchService.ISearchContext context) {}
    void UnityEditor.SearchService.ISelectorEngine.SetSearchFilter(UnityEditor.SearchService.ISearchContext context, string searchFilter) {}

    bool UnityEditor.SearchService.ISelectorEngine.SelectObject(
        UnityEditor.SearchService.ISearchContext context,
        Action<UnityEngine.Object, bool> selectHandler,
        Action<UnityEngine.Object> trackingHandler)
    {
        var selectContext = (UnityEditor.SearchService.ObjectSelectorSearchContext)context;
        if ((selectContext.visibleObjects & UnityEditor.SearchService.VisibleObjects.Assets) == 0)
            return false;

        if (!selectContext.requiredTypes.All(t => typeof(Material).IsAssignableFrom(t)))
            return false;

        if (!selectContext.editedObjects.All(o => o?.GetType().Name.Contains("decal", StringComparison.OrdinalIgnoreCase) ?? false))
            return false;

        var dbName = EnsureDecalPropertyIndexing();
        if (dbName == null)
            return false;

        #region ShowDecalPicker
        var query = SearchService.CreateContext(CreateDecalProvider(), $"a={dbName} t={selectContext.requiredTypeNames.First()} shader=Decal");
        var viewState = new SearchViewState(query, CreateDecalsTableConfiguration(),
            SearchViewFlags.TableView |
            SearchViewFlags.OpenInBuilderMode |
            SearchViewFlags.DisableSavedSearchQuery);
        viewState.windowTitle = new GUIContent("Material Decals", GetMaterialIcon());
        viewState.hideAllGroup = true;
        viewState.title = "decals";
        viewState.selectHandler = (item, canceled) => selectHandler(item?.ToObject(), canceled);
        viewState.trackingHandler = (item) => trackingHandler(item?.ToObject());
        viewState.position = SearchUtils.GetMainWindowCenteredPosition(new Vector2(600, 400));
        var pickerView = SearchService.ShowPicker(viewState);
        #endregion

        return pickerView != null;
    }

    #region CreateGroupProvider
    static SearchProvider CreateDecalProvider()
    {
        var assetProvider = SearchService.GetProvider("asset");
        var decalProvider = SearchUtils.CreateGroupProvider(assetProvider, "Decals", 0, true);
        decalProvider.fetchPropositions = EnumerateDecalPropositions;
        return decalProvider;
    }
    #endregion

    #region SearchProposition
    static IEnumerable<SearchProposition> EnumerateDecalPropositions(SearchContext context, SearchPropositionOptions options)
    {
        if (!options.flags.HasAny(SearchPropositionFlags.QueryBuilder))
            yield break;

        var shaderIcon = GetShaderIcon() as Texture2D;
        yield return new SearchProposition(category: "Affects", label: "Base Color", replacement: "affectalbedo=1", icon: shaderIcon);
        yield return new SearchProposition(category: "Affects", label: "Normal", replacement: "affectnormal=1", icon: shaderIcon);
        yield return new SearchProposition(category: "Affects", label: "Metal", replacement: "affectmetal=1", icon: shaderIcon);
        yield return new SearchProposition(category: "Affects", label: "Ambient Occlusion", replacement: "affectao=1", icon: shaderIcon);
        yield return new SearchProposition(category: "Affects", label: "Smoothness", replacement: "affectsmoothness=1", icon: shaderIcon);
        yield return new SearchProposition(category: "Affects", label: "Emission", replacement: "affectemission=1", icon: shaderIcon);
    }
    #endregion

    #region SearchTable
    static SearchTable CreateDecalsTableConfiguration()
    {
        return new SearchTable("decals", new SearchColumn[]
        {
            new SearchColumn("DecalsName0", "label", "name", new GUIContent("Name", GetMaterialIcon())) { width = 160 },
            new SearchColumn("DecalsShader1", "#shader", "name", new GUIContent("Shader", GetShaderIcon())) { width = 150 },
            new SearchColumn("DecalsBaseColor1", "#_BaseColor", "color", new GUIContent("Color", GetShaderIcon())) { width = 130 },
        });
    }
    #endregion

    #region CreateIndex
    static string EnsureDecalPropertyIndexing()
    {
        var materialDb = SearchService.EnumerateDatabases().FirstOrDefault(IsIndexingMaterialProperties);
        if (materialDb != null)
            return materialDb.name;

        if (!EditorUtility.DisplayDialog("Create decal material index",
            "Your project does not contain an index with decal material properties." +
            "\n\n" +
            "Do you want to create one now?", "Yes", "No"))
            return null;

        var dbName = "Decals";
        SearchService.CreateIndex(dbName,
            IndexingOptions.Properties | IndexingOptions.Dependencies |
            IndexingOptions.Types | IndexingOptions.Keep,
            roots: null,
            includes: new string[] { ".mat" },
            excludes: null,
            (name, path, finished) =>
            {
                Debug.Log($"Material index {name} created at {path}");
                finished();
            });
        return dbName;
    }

    static bool IsIndexingMaterialProperties(ISearchDatabase db)
    {
        if (string.Equals(db.name, "Materials", StringComparison.OrdinalIgnoreCase))
            return true;
        return (db.options & IndexingOptions.Properties) == IndexingOptions.Properties
            && (db.includes.Length == 0 || db.includes.Contains(".mat"));
    }
    #endregion
}
