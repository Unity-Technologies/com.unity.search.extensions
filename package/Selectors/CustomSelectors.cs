#if USE_SEARCH_TABLE
using UnityEngine;
using UnityEditor.Search;
using System.Collections.Generic;

static class CustomSelectors
{
    static Dictionary<string, long> s_TextureSizes = new Dictionary<string, long>();

    [SearchSelector("property_count", provider: "asset")]
    static object SelectPropertyCount(SearchSelectorArgs args)
    {
        Shader shader = args.current.ToObject<Shader>();
        if (!shader && args.current.ToObject() is Material material)
            shader = material.shader;

        if (shader)
            return shader.GetPropertyCount();

        return 0;
    }

    [SearchSelector("loc", provider: "asset")]
    static object SelectLineOfCode(SearchSelectorArgs args)
    {
        TextAsset textAsset = args.current.ToObject<TextAsset>();
        if (textAsset)
            return textAsset.text.Split('\n').Length;

        return null;
    }

    [SearchSelector("gsize")]
    static object SelectTextureGraphicSize(SearchSelectorArgs args)
    {
        var id = args.current.id;
        if (s_TextureSizes.TryGetValue(id, out long gsize))
            return gsize;
        var tex = args.current.ToObject<Texture>();
        if (!tex)
            return null;
        
        gsize = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex);
        s_TextureSizes[id] = gsize;
        return gsize;
    }
}
#endif
