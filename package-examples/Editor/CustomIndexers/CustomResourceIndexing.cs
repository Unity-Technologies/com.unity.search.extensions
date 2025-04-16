using UnityEditor.Search;

static class CustomResourceIndexing
{
    private const string kResourceRef = "resourcetyperef";

    [CustomObjectIndexer(typeof(ResourceProducer), version = 1)]
    internal static void IndexResourceProducer(CustomObjectIndexerTarget context, ObjectIndexer indexer)
    {
        var obj = context.target as ResourceProducer;
        if (obj == null || obj.recipes == null)
            return;

        foreach (var recipe in obj.recipes)
        {
            if (recipe.producedResources != null)
            {
                foreach (var res in recipe.producedResources)
                {
                    indexer.IndexProperty<ResourceType, ResourceProducer>(context.documentIndex, kResourceRef, res.ToString(), saveKeyword: false, exact: false);
                }
            }

            if (recipe.requiredResources != null)
            {
                foreach (var res in recipe.requiredResources)
                {
                    indexer.IndexProperty<ResourceType, ResourceProducer>(context.documentIndex, kResourceRef, res.ToString(), saveKeyword: false, exact: false);
                }
            }
        }
    }

    [CustomObjectIndexer(typeof(ResourceUser), version = 1)]
    internal static void IndexResourceUser(CustomObjectIndexerTarget context, ObjectIndexer indexer)
    {
        var obj = context.target as ResourceUser;
        if (obj == null)
            return;

        indexer.IndexProperty<ResourceType, ResourceUser>(context.documentIndex, kResourceRef, obj.requiredResource.ToString(), saveKeyword: false, exact: false);
    }

    [CustomObjectIndexer(typeof(ConstructedObject), version = 1)]
    internal static void IndexConstructedObject(CustomObjectIndexerTarget context, ObjectIndexer indexer)
    {
        var obj = context.target as ConstructedObject;
        if (obj == null || obj.requiredConstructionMaterials == null)
            return;

        foreach (var res in obj.requiredConstructionMaterials)
        {
            indexer.IndexProperty<ResourceType, ConstructedObject>(context.documentIndex, kResourceRef, res.ToString(), saveKeyword: false, exact: false);
        }
    }

    [CustomObjectIndexer(typeof(ResourceReserve), version = 1)]
    internal static void IndexResourceReserve(CustomObjectIndexerTarget context, ObjectIndexer indexer)
    {
        var obj = context.target as ResourceReserve;
        if (obj == null)
            return;

        indexer.IndexProperty<ResourceType, ResourceReserve>(context.documentIndex, kResourceRef, obj.resource.ToString(), saveKeyword: false, exact: false);
    }
}