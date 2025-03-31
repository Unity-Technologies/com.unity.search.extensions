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
            var tagPropertyName = $"{tagIdStr.ToLower()}";
            if (!string.IsNullOrEmpty(tagValue.name))
            {
                // Important Notes:
                // Use IndexProperty<PropertyType, PropertyTypeOwner> to ensure testismobilefriendly is available in the QueryBuilder.
                // Prefix <propertyname> with something (ex: the <PropertyOwnerType>) to have a unique property name that won't clash in the QueryBuilder
                // saveKeyword: false -> Ensure the index keyword list won't be polluted with the ALL keyword VALUES.
                // exact: false -> Ensure that we support variations (incomplete values) when searching.
                indexer.IndexProperty<string, Shader>(context.documentIndex, $"{nameof(Shader)}_tag.{tagPropertyName}", tagValue.name, saveKeyword:false, exact:false);
            }
        }
    }
}
