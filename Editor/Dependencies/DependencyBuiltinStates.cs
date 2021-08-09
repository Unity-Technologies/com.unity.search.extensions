#if USE_SEARCH_TABLE
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.Search
{
	static class DependencyBuiltinStates
	{
		static readonly List<string> emptySelection = new List<string>();

		[DependencyViewerProvider(DependencyViewerFlags.TrackSelection, name: "Selection")]
		internal static DependencyViewerState SelectionDependencies(DependencyViewerFlags flags)
		{
			return StateFromObjects("Selection", Selection.objects, flags | DependencyViewerFlags.All);
		}

		[DependencyViewerProvider(DependencyViewerFlags.TrackSelection, name: "Uses")]
		internal static DependencyViewerState SelectionUses(DependencyViewerFlags flags)
		{
			return StateFromObjects("Uses", Selection.objects, flags | DependencyViewerFlags.Uses);
		}

		[DependencyViewerProvider(DependencyViewerFlags.TrackSelection, name: "Used By")]
		internal static DependencyViewerState SelectionUsedBy(DependencyViewerFlags flags)
		{
			return StateFromObjects("Used By", Selection.objects, flags | DependencyViewerFlags.UsedBy);
		}

		[DependencyViewerProvider]
		internal static DependencyViewerState BrokenDependencies(DependencyViewerFlags flags)
		{
			var title = ObjectNames.NicifyVariableName(nameof(BrokenDependencies));
			return new DependencyViewerState(title,
				new DependencyState(title, SearchService.CreateContext("dep", "is:broken"))
			);
		}

		[DependencyViewerProvider]
		internal static DependencyViewerState MissingDependencies(DependencyViewerFlags flags)
		{
			var title = ObjectNames.NicifyVariableName(nameof(MissingDependencies));
			return new DependencyViewerState(title,
				new DependencyState(title, SearchService.CreateContext("dep", "is:missing"), new SearchTable("MissingDependencies", "Name", new[] {
					new SearchColumn("GUID", "label", "selectable") { width = 390 }
				}))
			);
		}

		[DependencyViewerProvider]
		internal static DependencyViewerState MostUsedAssets(DependencyViewerFlags flags)
		{
			var defaultDepFlags = SearchColumnFlags.CanSort | SearchColumnFlags.IgnoreSettings;
			var query = SearchService.CreateContext(new[] { "expression", "asset", "dep" }, "first{25,sort{select{p:a:assets, @path, count{dep:ref=\"@path\"}}, @value, desc}}");
			var title = ObjectNames.NicifyVariableName(nameof(MostUsedAssets));
			return new DependencyViewerState(title,
				new DependencyState(title, query, new SearchTable(title, "Name", new[] {
					new SearchColumn("Name", "label", "name", null, defaultDepFlags) { width = 390 },
					new SearchColumn("Count", "value", null, defaultDepFlags) { width = 80 }
				}))
			);
		}

		[DependencyViewerProvider]
		internal static DependencyViewerState UnusedAssets(DependencyViewerFlags flags)
		{
			var query = SearchService.CreateContext(new[] { "dep" }, "dep:in=0 is:file -is:package");
			var title = ObjectNames.NicifyVariableName(nameof(UnusedAssets));
			return new DependencyViewerState(title,
				new DependencyState(title, query, new SearchTable(title, "Name", new[] {
					new SearchColumn(title, "label", "Name") { width = 380 },
					new SearchColumn("Type", "type") { width = 90 },
					new SearchColumn("Size", "size", "size")  { width = 80 }
				}))
			);
		}

		internal static DependencyViewerState ObjectDependencies(UnityEngine.Object obj, bool showSceneRefs)
		{
			var flags = DependencyViewerFlags.All | (showSceneRefs ? DependencyViewerFlags.ShowSceneRefs : DependencyViewerFlags.None);
			var state = StateFromObjects(ObjectNames.NicifyVariableName(nameof(ObjectDependencies)), new[] { obj }, flags);
			state.name = ObjectNames.NicifyVariableName(nameof(ObjectDependencies));
			return state;
		}

		#if !UNITY_2021
        internal static DependencyViewerState CreateStateFromQuery(SearchQueryAsset sqa)
		{
			return new DependencyViewerState(sqa.name, new[] { new DependencyState(sqa) })
			{
				description = new GUIContent(sqa.searchText)
			};
		}
		#endif

		static DependencyViewerState EmptySelection(string name)
        {
			var state = new DependencyViewerState(name, emptySelection);
			state.flags |= DependencyViewerFlags.TrackSelection;
			return state;
		}

		static DependencyViewerState StateFromObjects(string stateName, IEnumerable<UnityEngine.Object> objects, DependencyViewerFlags flags)
		{
			if (!objects.Any())
				return EmptySelection(stateName);

			var globalObjectIds = new List<string>();
			var selectedPaths = new List<string>();
			var selectedInstanceIds = new List<int>();
			foreach (var obj in objects)
			{
				var instanceId = obj.GetInstanceID();
				var assetPath = AssetDatabase.GetAssetPath(instanceId);
				if (!string.IsNullOrEmpty(assetPath))
				{
					if (System.IO.Directory.Exists(assetPath))
						continue;
					selectedPaths.Add("\"" + assetPath + "\"");
				}
				else
					selectedInstanceIds.Add(instanceId);
				globalObjectIds.Add(GlobalObjectId.GetGlobalObjectIdSlow(instanceId).ToString());
			}

			if (globalObjectIds.Count == 0)
				return EmptySelection(stateName);

			var fetchSceneRefs = flags.HasFlag(DependencyViewerFlags.ShowSceneRefs);
			var providers = fetchSceneRefs ? new[] { "expression", "dep", "scene" } : new[] { "expression", "dep" };
			var selectedPathsStr = string.Join(",", selectedPaths);
			var fromQuery = $"from=[{selectedPathsStr}]";
			if (selectedInstanceIds.Count > 0)
			{
                var selectedInstanceIdsStr = string.Join(",", selectedInstanceIds);
                fromQuery = $"{{{fromQuery}, deps{{[{selectedInstanceIdsStr}], {fetchSceneRefs}}}}}";
                selectedPathsStr = string.Join(",", selectedPaths.Concat(selectedInstanceIds.Select(e => e.ToString())));
			}
			var state = new DependencyViewerState(stateName, globalObjectIds) { flags = flags | DependencyViewerFlags.TrackSelection };
			if (selectedInstanceIds.Count == 1)
			{
				var selectedObject = EditorUtility.InstanceIDToObject(selectedInstanceIds.First());
				var thumbnail = AssetPreview.GetMiniThumbnail(selectedObject);
				state.windowTitle = new GUIContent(selectedObject.name, thumbnail);
				if (selectedObject is GameObject go)
					state.description = new GUIContent(SearchUtils.GetHierarchyPath(go, true), thumbnail);
			}
			if (flags.HasFlag(DependencyViewerFlags.Uses))
				state.states.Add(new DependencyState("Uses", SearchService.CreateContext(providers, fromQuery)));
			if (flags.HasFlag(DependencyViewerFlags.UsedBy))
				state.states.Add(new DependencyState("Used By", SearchService.CreateContext(providers, $"ref=[{selectedPathsStr}]")));
			return state;
		}
	}
}
#endif
