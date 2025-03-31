using UnityEditor.Search;
using UnityEngine;

public static class TextureCustomIndexing
{
    const int version = 1;

    [CustomObjectIndexer(typeof(Texture2D))]
    static void IndexMobileFriendlyTexture(CustomObjectIndexerTarget target, ObjectIndexer indexer)
    {
        var texture = target.target as Texture2D;
        if (texture == null)
            return;

        bool isMobileFriendly = texture.width < 64 && texture.height < 64;

        // Important Notes:
        // Use IndexProperty<PropertyType, PropertyTypeOwner> to ensure testismobilefriendly is available in the QueryBuilder.
        // Prefix propertyname with something (ex: the <PropertyOwnerType>) to have a unique property name that won't clash in the QueryBuilder
        // saveKeyword: false -> Ensure the index keyword list won't be polluted with the keyword VALUES.
        // exact: false -> Ensure that we support variations (incomplete values) when searching.
        indexer.IndexProperty<bool, Texture2D>(target.documentIndex, "Texture2D.testismobilefriendly", isMobileFriendly.ToString(), saveKeyword: false, exact: false);
    }
}