using UnityEngine;
using UnityEditor;
using UnityEditor.Search;

static class MaterialReferencesIndexer
{
	const int version = 3;

	[CustomObjectIndexer(typeof(MeshRenderer), version = version)]
	public static void IndexMeshRendererMaterialReferences(CustomObjectIndexerTarget context, ObjectIndexer indexer)
	{
		var c = context.target as MeshRenderer;
		if (c == null)
			return;

		foreach (var m in c.sharedMaterials)
		{
			if (!m)
				continue;

			// Index material name reference
			if (!string.IsNullOrEmpty(m.name))
				indexer.AddProperty("ref", m.name.Replace(" (Instance)", "").ToLowerInvariant(), context.documentIndex);

			// Index material asset path reference
			IndexObjectAssetPathReference(m, context, indexer);
			
			if (m.shader != null)
			{
				// Index shader name reference
				indexer.AddProperty("ref", m.shader.name.ToLowerInvariant(), context.documentIndex);

				// Index shader name reference
				IndexObjectAssetPathReference(m.shader, context, indexer);
			}
		}
	}

	static void IndexObjectAssetPathReference(Object obj, CustomObjectIndexerTarget context, ObjectIndexer indexer)
	{
		var objectPath = AssetDatabase.GetAssetPath(obj);
		if (!string.IsNullOrEmpty(objectPath))
			indexer.AddProperty("ref", objectPath.ToLowerInvariant(), context.documentIndex);
	}
}
