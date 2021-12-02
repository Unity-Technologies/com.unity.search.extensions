#if !USE_SEARCH_DEPENDENCY_VIEWER || USE_SEARCH_MODULE
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.Search
{
    static class DependencyBuiltinStates
    {
        static readonly List<string> emptySelection = new List<string>();

        [DependencyViewerProvider(DependencyViewerFlags.TrackSelection, name: "Selection")]
        internal static DependencyViewerState SelectionDependencies(DependencyViewerConfig config)
        {
            config.flags |= DependencyViewerFlags.All;
            return StateFromObjects("Selection", Selection.objects, config);
        }

        [DependencyViewerProvider(DependencyViewerFlags.TrackSelection, name: "Uses")]
        internal static DependencyViewerState SelectionUses(DependencyViewerConfig config)
        {
            config.flags |= DependencyViewerFlags.Uses;
            return StateFromObjects("Uses", Selection.objects, config);
        }

        [DependencyViewerProvider(DependencyViewerFlags.TrackSelection, name: "Used By")]
        internal static DependencyViewerState SelectionUsedBy(DependencyViewerConfig config)
        {
            config.flags |= DependencyViewerFlags.UsedBy;
            return StateFromObjects("Used By", Selection.objects, config);
        }

        [DependencyViewerProvider]
        internal static DependencyViewerState BrokenDependencies(DependencyViewerConfig config)
        {
            var title = ObjectNames.NicifyVariableName(nameof(BrokenDependencies));
            return new DependencyViewerState(title,
                new DependencyState(title, SearchService.CreateContext("dep", "is:broken"))
            );
        }

        [DependencyViewerProvider]
        internal static DependencyViewerState MissingDependencies(DependencyViewerConfig config)
        {
            var title = ObjectNames.NicifyVariableName(nameof(MissingDependencies));
            return new DependencyViewerState(title,
                new DependencyState(title, SearchService.CreateContext("dep", "is:missing"), new SearchTable("MissingDependencies", "Name", new[] {
                    new SearchColumn("GUID", "label", "selectable") { width = 390 }
                }))
            );
        }

        [DependencyViewerProvider]
        internal static DependencyViewerState MostUsedAssets(DependencyViewerConfig config)
        {
            var defaultDepFlags = SearchColumnFlags.CanSort | SearchColumnFlags.IgnoreSettings;
            var query = SearchService.CreateContext(new[] { "expression", "asset", "dep" }, Dependency.GetMostUsedAssetsQuery());
            var title = ObjectNames.NicifyVariableName(nameof(MostUsedAssets));
            return new DependencyViewerState(title,
                new DependencyState(title, query, new SearchTable(title, "Name", new[] {
                    new SearchColumn("Name", "label", "name", null, defaultDepFlags) { width = 390 },
                    new SearchColumn("Count", "value", null, defaultDepFlags) { width = 80 }
                }))
            );
        }

        [DependencyViewerProvider]
        internal static DependencyViewerState UnusedAssets(DependencyViewerConfig config)
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

        [DependencyViewerProvider]
        internal static DependencyViewerState IgnoredAssets(DependencyViewerConfig config)
        {
            var query = SearchService.CreateContext(new[] { "adb" }, $"l:{Dependency.ignoreDependencyLabel}");
            var title = ObjectNames.NicifyVariableName(nameof(IgnoredAssets));
            return new DependencyViewerState(title,
                new DependencyState(title, query, new SearchTable(title, "Name", new[] {
                    new SearchColumn(title, "label", "Name") { width = 380 },
                    new SearchColumn("Type", "type") { width = 90 },
                    new SearchColumn("Size", "size", "size")  { width = 80 }
                }))
            );
        }

        internal static DependencyViewerState ObjectDependencies(UnityEngine.Object obj, DependencyViewerConfig config)
        {
            config.flags |= DependencyViewerFlags.All;
            var state = StateFromObjects(ObjectNames.NicifyVariableName(nameof(ObjectDependencies)), new[] { obj }, config);
            state.name = ObjectNames.NicifyVariableName(nameof(ObjectDependencies));
            return state;
        }

        #if !USE_SEARCH_EXTENSION_API
         internal static DependencyViewerState CreateStateFromQuery(SearchQueryAsset sqa)
        {
            return new DependencyViewerState(sqa.name, new[] { new DependencyState(sqa) })
            {
                description = new GUIContent(sqa.searchText)
            };
        }
        #else
        internal static DependencyViewerState CreateStateFromQuery(ISearchQuery sqa)
        {
            return new DependencyViewerState(sqa.GetName(), new[] { new DependencyState(sqa) })
            {
                description = new GUIContent(sqa.searchText)
            };
        }
        #endif

        static DependencyViewerState EmptySelection(string name)
        {
            var state = new DependencyViewerState(name, emptySelection);
            state.config = new DependencyViewerConfig(DependencyViewerFlags.TrackSelection);
            return state;
        }

        static DependencyViewerState StateFromObjects(string stateName, IEnumerable<UnityEngine.Object> objects, DependencyViewerConfig config)
        {
            if (!objects.Any())
                return EmptySelection(stateName);

            var globalObjectIds = new List<string>();
            var selectedPaths = new List<string>();
            var selectedInstanceIds = new List<int>();
            foreach (var obj in objects)
            {
                if (!obj)
                    continue;
                var instanceId = obj.GetInstanceID();
                var assetPath = AssetDatabase.GetAssetPath(instanceId);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    if (System.IO.Directory.Exists(assetPath))
                        continue;
                    selectedPaths.Add($"\"{assetPath}\"");
                }
                else
                    selectedInstanceIds.Add(instanceId);
                globalObjectIds.Add(GlobalObjectId.GetGlobalObjectIdSlow(instanceId).ToString());
            }

            if (globalObjectIds.Count == 0)
                return EmptySelection(stateName);

            var fetchSceneRefs = config.flags.HasFlag(DependencyViewerFlags.ShowSceneRefs);
            var providers = fetchSceneRefs ? new[] { "expression", "dep", "scene" } : new[] { "expression", "dep" };
            var query = Dependency.CreateUsingQuery(selectedPaths, config.depthLevel - 1);
            if (selectedInstanceIds.Count > 0)
            {
                var selectedInstanceIdsStr = string.Join(",", selectedInstanceIds);
                query = $"union{{{query}, deps{{[{selectedInstanceIdsStr}], {fetchSceneRefs}}}}}";
                selectedPaths.AddRange(selectedInstanceIds.Select(e => e.ToString()));
            }

            config.flags |= DependencyViewerFlags.TrackSelection;
            var state = new DependencyViewerState(stateName, globalObjectIds) { config = config };
            if (selectedInstanceIds.Count == 1)
            {
                var selectedObject = EditorUtility.InstanceIDToObject(selectedInstanceIds.First());
                var thumbnail = AssetPreview.GetMiniThumbnail(selectedObject);
                state.windowTitle = new GUIContent(selectedObject.name, thumbnail);
                if (selectedObject is GameObject go)
                    state.description = new GUIContent(go.name, thumbnail);
            }
            if (config.flags.HasFlag(DependencyViewerFlags.Uses))
            {
                state.states.Add(new DependencyState("Uses", SearchService.CreateContext(providers, query))
                {
                    supportsDepth = true
                });
            }
            if (config.flags.HasFlag(DependencyViewerFlags.UsedBy))
                state.states.Add(new DependencyState("Used By", SearchService.CreateContext(providers, $"ref=[{string.Join(",", selectedPaths)}]")));
            return state;
        }
    }
}
#endif
