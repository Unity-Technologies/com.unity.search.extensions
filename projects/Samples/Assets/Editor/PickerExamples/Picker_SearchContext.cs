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

    [SearchContext("t:script", "adb", SearchViewFlags.ListView)]
    public MonoScript myProjectScript;

    [SearchContext("script", "adb", SearchViewFlags.Packages | SearchViewFlags.CompactView)]
    public MonoScript myPackageScript;

    [SearchContext("t:texture", assetProviders, SearchViewFlags.GridView)]
    public Texture myTexture;
    
    [SearchContext("t:mesh is:nested mesh", "asset")]
    public UnityEngine.Object assetMesh;

    [SearchContext("h:cube", objectProviders)]
    public MeshFilter sceneMesh;

    [SearchContext("shader:standard", assetProviders, SearchViewFlags.HideSearchBar)]
    public Material materialNoSearchBar;

    [SearchContext("select{p:t:material, @label, @size}", objectProviders, SearchViewFlags.TableView)]
    public Material selectMaterial;

    [SearchContext("Assets/Queries/textures.asset", assetProviders)]
    public Texture searchQueryPathTexture;

    [SearchContext("3c7f5dff3fb5d724688dfcecfb131b2a", assetProviders)]
    public Texture searchQueryGuidTexture;

    [SearchContext("t:currentobject{@type, 'texture'}", "asset")]
    public UnityEngine.Object myObjectWithContext;

    [SearchContext("p: t:<$list:Texture2D, [Texture2D, Material, Prefab]$>", "asset", SearchViewFlags.OpenInBuilderMode | SearchViewFlags.DisableBuilderModeToggle)]
    public UnityEngine.Object myObjectOfConstrainedTypes;

    [SearchContext("my search", new[] { typeof(MyTextureProvider) })]
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
