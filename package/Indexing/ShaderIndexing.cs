using UnityEngine;
using UnityEditor.Search;

public static class ShaderIndexing
{
    const int version = 1;

    public static string[] kTagIds = new string [] 
    {
        "LIGHTMODE",
        "SHADOWCASTER",
        "SHADOWCOLLECTOR",

        "Vertex",
        "VertexLM",
        "VertexLMRGBM",
        "REQUIREOPTIONS",
        "FORCENOSHADOWCASTING",
        "IGNOREPROJECTOR",
        "SHADOWSUPPORT",
        "PASSFLAGS",
        "RenderType",
        "DisableBatching",
        "LodFading",
        "RenderPipeline",
        "Picking",
        "SceneSelectionPass",
    };

    [CustomObjectIndexer(typeof(Shader), version = version)]
    public static void IndexShaderTagProperties(CustomObjectIndexerTarget context, ObjectIndexer indexer)
    {
        if (!(context.target is Shader shader))
            return;

        foreach(var tagIdStr in kTagIds)
        {
            var tagId = new UnityEngine.Rendering.ShaderTagId(tagIdStr);
            var tagValue = shader.FindSubshaderTagValue(0, tagId);
            var tagPropertyName = $"sh_{tagIdStr.ToLower()}";
            if (!string.IsNullOrEmpty(tagValue.name))
                indexer.IndexProperty(context.documentIndex, tagPropertyName, tagValue.name, true);
        }

    }
}
