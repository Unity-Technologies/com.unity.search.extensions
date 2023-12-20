using System.Collections;
using System.Collections.Generic;
using UnityEditor.Search;
using UnityEditor;
using UnityEngine;
using UnityEngine.Search;

public class Picker_SearchContext : MonoBehaviour
{
    const string assetProviders = "adb;asset";
    const string objectProviders = "adb,asset,scene";
    const SearchViewFlags minimalUIViewFlags = SearchViewFlags.OpenInBuilderMode |
        SearchViewFlags.Packages |
        SearchViewFlags.DisableSavedSearchQuery |
        SearchViewFlags.GridView;

    [SearchContext("t:script", "asset", SearchViewFlags.ListView)]
    public MonoScript myProjectScript;

    [SearchContext("t:script overlay", "adb", SearchViewFlags.Packages | SearchViewFlags.CompactView)]
    public MonoScript myPackageScript;

    [SearchContext("t:sprite", assetProviders, minimalUIViewFlags)]
    public Sprite mySprite;
    
    [SearchContext("h:cube", objectProviders)]
    public MeshFilter sceneMesh;

    // Find all material with a shader that contains the word "New"
    [SearchContext("shader:New", assetProviders, SearchViewFlags.HideSearchBar)]
    public Material materialNoSearchBar;
    
    // Open Picker with a preloaded SearchQueryAsset specified by its path
    [SearchContext("Assets/Queries/t_sprite.asset", assetProviders)]
    public Sprite searchQueryPathSprite;

    // Open Picker with a preloaded SearchQueryAsset specified by its guid
    [SearchContext("40060e4225366c64a9e24cd17cc9fdc1", assetProviders)]
    public Sprite searchQueryGuidSprite;

    [SearchContext("p: t:<$list:Texture2D, [Texture2D, Material, Shader]$>", "asset", SearchViewFlags.OpenInBuilderMode | SearchViewFlags.DisableBuilderModeToggle)]
    public UnityEngine.Object myObjectOfConstrainedTypes;

    // Use a custom SearhcProvider to find an alien texture
    [SearchContext("alien", new[] { typeof(MyTextureProvider) })]
    public Texture2D mySpecialTexture2D;

    class MyTextureProvider : SearchProvider
    {
        static string ProviderId = "myTexture";

        public MyTextureProvider()
            : base(ProviderId)
        {
            fetchItems = (context, items, provider) => SearchItems(context, provider);
            fetchLabel = (item, context) =>
            {
                var assetPath = AssetDatabase.GUIDToAssetPath((string)item.data);
                return GetNameFromPath(assetPath);
            };
            fetchThumbnail = (item, context) =>
            {
                var obj = toObject(item, typeof(Texture2D));
                return AssetPreview.GetAssetPreview(obj);
            };
            toObject = (item, type) =>
            {
                var assetPath = AssetDatabase.GUIDToAssetPath((string)item.data);
                return AssetDatabase.LoadAssetAtPath(assetPath, type);
            };
        }

        static IEnumerator SearchItems(SearchContext context, SearchProvider provider)
        {
            foreach (var texture2DGuid in GetMyTextures())
            {
                var path = AssetDatabase.GUIDToAssetPath(texture2DGuid);
                if (path != null && path.Contains(context.searchText, System.StringComparison.InvariantCultureIgnoreCase))
                    yield return provider.CreateItem(context, texture2DGuid, texture2DGuid.GetHashCode(), null, null, null, texture2DGuid);
            }
        }

        static IEnumerable<string> GetMyTextures()
        {
            return AssetDatabase.FindAssets("t:texture2d");
        }

        static string GetNameFromPath(string path)
        {
            var lastSep = path.LastIndexOf('/');
            if (lastSep == -1)
                return path;

            return path.Substring(lastSep + 1);
        }
    }
}
