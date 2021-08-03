using UnityEngine;
using UnityEditor.Search;
using UnityEditor.Search.Providers;

static class CustomSelectors
{
	[SceneQueryEngineFilter("vertices")]
	internal static float? FilterMeshRendererMaterials(GameObject go)
	{
		#pragma warning disable UNT0008 // Null propagation on Unity objects
		var meshFilter = go?.GetComponent<MeshFilter>();
		#pragma warning restore UNT0008 // Null propagation on Unity objects
		if (!meshFilter || !meshFilter.sharedMesh)
			return null;
		return meshFilter.sharedMesh.vertexCount;
	}

	#if USE_SEARCH_TABLE

	[SearchSelector("vertices", provider: "scene")]
	static object SelectVertices(SearchSelectorArgs args)
	{
		var go = args.current.ToObject<GameObject>();
		if (!go)
			return null;

		var meshFilter = go.GetComponent<MeshFilter>();
		if (!meshFilter || !meshFilter.sharedMesh)
			return null;

		return meshFilter.sharedMesh.vertexCount;
	}

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

	#endif
}
