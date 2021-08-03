using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.Search
{
	[ExcludeFromPreset]
	class DependencyInfo : ScriptableObject, IDisposable
	{
		bool disposed;
		public string guid;
		public List<string> broken = new List<string>();
		public List<Object> @using = new List<Object>();
		public List<Object> usedBy = new List<Object>();
		public List<string> untracked = new List<string>();

		protected virtual void Dispose(bool disposing)
		{
			if (disposed)
				return;
			broken.Clear();
			@using.Clear();
			usedBy.Clear();
			untracked.Clear();
			DestroyImmediate(this);
			disposed = true;
		}

		~DependencyInfo()
		{
			Dispose(disposing: false);
		}

		public void Dispose()
		{
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}

		public void Load(in SearchItem item)
		{
			var providers = new string[] { Dependency.providerId };
			guid = item.id;
			using (var context = SearchService.CreateContext(providers, $"from=\"{item.id}\""))
			{
				foreach (var r in SearchService.GetItems(context, SearchFlags.Synchronous))
				{
					var assetPath = AssetDatabase.GUIDToAssetPath(r.id);
					if (string.IsNullOrEmpty(assetPath))
						broken.Add(r.id);
					else
					{
						var ur = AssetDatabase.LoadMainAssetAtPath(assetPath);
						if (ur != null)
							@using.Add(ur);
						else
							untracked.Add($"{assetPath} ({r.id})");
					}
				}
			}

			using (var context = SearchService.CreateContext(providers, $"ref=\"{item.id}\""))
			{
				foreach (var r in SearchService.GetItems(context, SearchFlags.Synchronous))
				{
					var assetPath = AssetDatabase.GUIDToAssetPath(r.id);
					if (string.IsNullOrEmpty(assetPath))
						broken.Add(r.id);
					else
					{
						{
							var ur = AssetDatabase.LoadMainAssetAtPath(assetPath);
							if (ur != null)
								usedBy.Add(ur);
							else
								untracked.Add($"{assetPath} ({r.id})");
						}
					}
				}
			}
		}
	}
}
