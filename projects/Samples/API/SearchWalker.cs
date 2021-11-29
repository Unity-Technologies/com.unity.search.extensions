#if UNITY_2021_2_OR_NEWER
using UnityEngine;
using UnityEditor;
using UnityEditor.Search;
using UnityEditor.Search.Providers;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System;

using Object = UnityEngine.Object;
using System.Reflection;

static class SearchWalker
{
	[MenuItem("Window/Search/Search And Update")]
	public static void ExecuteSearchUpdate()
	{
		// IMPORTANT: Make sure to have a proper index setup.

		ProgressUtility.RunTask("Search Update", "Running search queries to update your project...", RunSearchUpdate);
	}

	static IEnumerator RunSearchUpdate(int progressId, object userData)
	{
		Progress.Report(progressId, -1);

		// Find input materials
		var materialPaths = new HashSet<string>();
		yield return FindAssets(progressId, "*.mat", materialPaths);

		// For each material, find object references
		int processCount = 0;
		var objectIds = new List<PatchItem>();
		foreach (var matPath in materialPaths)
		{
			Debug.Log($"<color=#23E55A>Fetching objects...</color> {matPath}");
			Progress.Report(progressId, processCount++, materialPaths.Count, matPath);
			yield return EnumerateGlobalObjectIds(matPath, objectIds);
		}

		Progress.Report(progressId, -1, "Sorting objects...");
		objectIds = objectIds.OrderBy(pi => pi.gid.assetGUID.ToString()).Distinct().ToList();

		processCount = 0;
		foreach (var pi in EnumerateObjects(objectIds))
		{
			if (pi == null)
			{
				yield return null;
				continue;
			}

			// TODO: Patch object
			Debug.Log($"<color=#23E55A>Patching</color> {pi.obj} {{{pi.gid}}}...");
			Progress.Report(progressId, processCount++, objectIds.Count, pi.source);
			yield return null;
			yield return null;
			yield return null;
		}
	}

	static IEnumerator FindAssets(int progressId, string query, ICollection<string> filePaths)
	{
		using (var context = SearchService.CreateContext("find", query))
		using (var request = SearchService.Request(context))
		{
			foreach (var r in request)
			{
				if (r == null)
				{
					yield return null;
					continue;
				}

				var assetPath = UnityEditor.Search.SearchUtils.GetAssetPath(r);
				filePaths.Add(assetPath);
				Progress.Report(progressId, filePaths.Count, request.Count, assetPath);
			}
		}
	}

	static IEnumerable<Object> EnumerateGlobalObjectIds(string source, ICollection<PatchItem> ids)
	{
		using (var context = SearchService.CreateContext("asset", $"ref=\"{source}\""))
		using (var request = SearchService.Request(context))
		{
			foreach (var r in request)
			{
				if (r == null)
				{
					yield return null;
					continue;
				}

				if (GlobalObjectId.TryParse(r.id, out var gid))
					ids.Add(new PatchItem(source, gid));
			}
		}
	}

	static IEnumerable<PatchItem> EnumerateObjects(ICollection<PatchItem> ids)
	{
		foreach (var pi in ids)
		{
			var gid = pi.gid;
			var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
			if (!obj)
			{
				// Open container scene
				if (gid.identifierType == (int)IdentifierType.kSceneObject)
				{
					var containerPath = AssetDatabase.GUIDToAssetPath(gid.assetGUID);

					var mainInstanceID = GetMainAssetInstanceID(containerPath);
					AssetDatabase.OpenAsset(mainInstanceID);
					yield return null;

					var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
					while (!scene.isLoaded)
						yield return null;
				}

				// Reload object
				obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
				if (!obj)
				{
					Debug.LogError($"<color=#E5455A>Failed to patch</color> {gid}");
					continue;
				}
			}

			yield return new PatchItem(pi.source, gid, obj);
		}
	}

	static MethodInfo s_GetMainAssetInstanceID;
	static int GetMainAssetInstanceID(string assetPath)
	{
        if (s_GetMainAssetInstanceID == null)
        {
            var type = typeof(AssetDatabase);
            s_GetMainAssetInstanceID = type.GetMethod("GetMainAssetInstanceID", BindingFlags.NonPublic | BindingFlags.Static);
            if (s_GetMainAssetInstanceID == null)
                return default;
        }
        object[] parameters = new object[] { assetPath };
        return (int)s_GetMainAssetInstanceID.Invoke(null, parameters);
	}

	enum IdentifierType { kNullIdentifier = 0, kImportedAsset = 1, kSceneObject = 2, kSourceAsset = 3, kBuiltInAsset = 4 };

	class PatchItem : IEquatable<PatchItem>
	{
		public readonly string source;
		public readonly GlobalObjectId gid;
		public readonly Object obj;

		public PatchItem(string source, GlobalObjectId gid)
			: this(source, gid, null)
		{
		}

		public PatchItem(string source, GlobalObjectId gid, Object obj)
		{
			this.source = source;
			this.gid = gid;
			this.obj = obj;
		}

		public override bool Equals(object other)
		{
			return other is PatchItem l && Equals(l);
		}

		public override int GetHashCode()
		{
			return gid.GetHashCode();
		}

		public bool Equals(PatchItem other)
		{
			return string.Equals(this.gid.ToString(), other.gid.ToString(), StringComparison.Ordinal);
		}
	}
}
#endif
