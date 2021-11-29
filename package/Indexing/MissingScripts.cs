#if UNITY_2021_2_OR_NEWER && INDEX_MISSING_SCRIPTS
//#define DEBUG_MISSING_SCRIPT_INDEXING
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityEditor.Search
{
    static class MissingScripts
    {
        #if USE_QUERY_BUILDER
        [QueryListBlock("Source", "from", "from", "=")]
        class FromSourceListBlock : QueryListBlock
        {
            public FromSourceListBlock(IQuerySource source, string id, string value, QueryListBlockAttribute attr)
                : base(source, id, value, attr)
            {
            }

            public override IEnumerable<SearchProposition> GetPropositions(SearchPropositionFlags flags)
            {
                yield return CreateProposition(flags, "Prefab", "prefab", "Source if a prefab asset");
                yield return CreateProposition(flags, "Scene", "scene", "Source if a scene asset");
            }
        }

        [QueryListBlock("Missing Dependencies", "missing", "missing", ":")]
        class MissingListBlock : QueryListBlock
        {
            public MissingListBlock(IQuerySource source, string id, string value, QueryListBlockAttribute attr)
                : base(source, id, value, attr)
            {
            }

            public override IEnumerable<SearchProposition> GetPropositions(SearchPropositionFlags flags)
            {
                yield return CreateProposition(flags, "Prefab", "prefab");
                yield return CreateProposition(flags, "Scripts", "Scripts");
            }
        }

        [SearchPropositionsProvider]
        internal static IEnumerable<SearchProposition> FetchMissingScriptsPropositions(SearchContext context, SearchPropositionOptions options)
        {
            var sceneIcon = Utils.LoadIcon("SceneAsset Icon");
            yield return new SearchProposition(category: "Missing Scripts", "Broken Assets", "is:broken", "List all assets that is broken (i.e. missing scripts).", icon: sceneIcon, priority: -1);
            yield return new SearchProposition(category: "Missing Scripts", "Missing Scripts", "missing:scripts", "Find nested objects with missing scripts.", icon: sceneIcon, priority: -1);
            yield return new SearchProposition(category: "Missing Scripts", "From Prefabs", "from:prefab", "List all objects nested in a prefab", icon: sceneIcon);
            yield return new SearchProposition(category: "Missing Scripts", "From Scenes", "from:scene", "List all objects nested in a scene.", icon: sceneIcon);
            yield return new SearchProposition(category: "Missing Scripts", "Scene source for nested objects", "scene=<$object:none,SceneAsset$>", "Find all nested objects referencing a specific scene.", icon: sceneIcon);
        }

        #endif

        #if DEBUG_MISSING_SCRIPT_INDEXING
        [MenuItem("Assets/Index Missing Scripts")]
        internal static void IndexMissingScriptSelectedAsset()
        {
            if (Selection.assetGUIDs.Length == 0)
                return;
            IndexMissingScripts(AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]));
        }
        #endif

        [CustomObjectIndexer(typeof(SceneAsset), version = 1)]
        internal static void IndexSceneMissingScripts(CustomObjectIndexerTarget context, ObjectIndexer indexer)
        {
            IndexMissingScripts(context.id, context.documentIndex, indexer);
        }

        [CustomObjectIndexer(typeof(GameObject), version = 1)]
        internal static void IndexPrefabScripts(CustomObjectIndexerTarget context, ObjectIndexer indexer)
        {
            IndexMissingScripts(context.id, context.documentIndex, indexer);
        }

        static void IndexMissingScripts(in string assetPath, int documentIndex = -1, ObjectIndexer indexer = null)
        {
            if (assetPath.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))
            {
                var scene = EditorSceneManager.OpenScene(assetPath, OpenSceneMode.Additive);
                while (!scene.isLoaded)
                    ;
                IndexMissingScripts(scene, documentIndex, indexer);
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
            else if (assetPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            {
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) is GameObject prefab)
                {
                    var prefabGameObjects = new List<GameObject>();
                    MergeObjects(prefabGameObjects, prefab.transform, false);
                    Log($"Indexing missing scripts for prefab {assetPath} containing {prefabGameObjects.Count} objects");
                    IndexMissingScripts(prefabGameObjects, documentIndex, indexer);
                }
            }
        }

        static void MergeObjects(IList<GameObject> objects, Transform transform, bool skipSelf)
        {
            if (!transform || !transform.gameObject || (transform.gameObject.hideFlags & (HideFlags.DontSave | HideFlags.HideInInspector)) != 0)
                return;
            if (!skipSelf || transform.childCount == 0)
                objects.Add(transform.gameObject);
            for (int c = 0, end = transform.childCount; c != end; ++c)
                MergeObjects(objects, transform.GetChild(c), false);
        }

        static void IndexMissingScripts(in UnityEngine.SceneManagement.Scene scene, int documentIndex = -1, ObjectIndexer indexer = null)
        {
            var gameObjects = SearchUtils.FetchGameObjects(scene).ToList();
            Log($"Indexing missing scripts for {scene.path} containing {gameObjects.Count} objects");
            IndexMissingScripts(gameObjects, documentIndex, indexer);
        }

        static void IndexMissingScripts(IEnumerable<GameObject> gameObjects, int documentIndex = -1, ObjectIndexer indexer = null)
        {
            foreach (var go in gameObjects)
            {
                if (!CanHaveMissingScripts(go))
                    continue;

                IndexMissingScripts(go, documentIndex, indexer);
            }
        }

        static void IndexMissingScripts(GameObject go, int documentIndex = -1, ObjectIndexer indexer = null)
        {
            int goDocumentIndex = -1;
            bool hasMissingScripts = false;

            if (go.scene.path != null && PrefabUtility.IsPartOfPrefabAsset(go))
                return;

            var components = go.GetComponents<MonoBehaviour>();
            foreach (var c in components)
            {
                if (c)
                    continue;

                if (PrefabUtility.IsPartOfPrefabInstance(go) && !PrefabUtility.HasPrefabInstanceAnyOverrides(go, false))
                    continue;

                if (PrefabUtility.IsPartOfPrefabInstance(go))
                {
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(go) && !PrefabUtility.IsPartOfPrefabThatCanBeAppliedTo(go))
                        continue;
                }

                hasMissingScripts = true;
                Log($"Missing scripts for {go} in {GetObjectSourcePath(go)}", go);
                if (indexer != null)
                {
                    if (GetBrokenObjectDocumentIndex(ref goDocumentIndex, go, indexer))
                        indexer.IndexProperty(goDocumentIndex, "missing", "scripts", saveKeyword: true, exact: true);
                }
            }

            if (PrefabUtility.IsPrefabAssetMissing(go))
            {
                hasMissingScripts = true;
                Log($"Missing prefab asset for {go} in {go.scene.path}", go);
                if (GetBrokenObjectDocumentIndex(ref goDocumentIndex, go, indexer))
                    indexer?.AddProperty("missing", "prefab", goDocumentIndex, saveKeyword: true, exact: true);
            }

            if (hasMissingScripts && documentIndex != -1)
                indexer?.AddProperty("is", "broken", documentIndex, saveKeyword: true, exact: true);
        }

        static bool GetBrokenObjectDocumentIndex(ref int goDocumentIndex, GameObject go, ObjectIndexer indexer)
        {
            if (indexer == null)
                return false;
            if (goDocumentIndex != -1)
                return true;
            var gid = GlobalObjectId.GetGlobalObjectIdSlow(go);
            var sourcePath = GetObjectSourcePath(go);
            goDocumentIndex = indexer.AddDocument(gid.ToString(), SearchUtils.GetHierarchyPath(go, false), sourcePath,
                checkIfExists: true, SearchDocumentFlags.Nested | SearchDocumentFlags.Object);

            var indexingSource = GetFromIndexingType(go);
            indexer.IndexProperty(goDocumentIndex, "from", indexingSource, saveKeyword: true, exact: true);
            indexer.AddReference(goDocumentIndex, indexingSource, AssetDatabase.LoadMainAssetAtPath(sourcePath));
            return true;
        }

        static string GetFromIndexingType(GameObject go)
        {
            if (go.scene.path != null)
                return "scene";
            return "prefab";
        }

        static string GetObjectSourcePath(GameObject go)
        {
            return go.scene.path ?? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
        }

        static bool CanHaveMissingScripts(Object obj)
        {
            if (PrefabUtility.GetPrefabInstanceStatus(obj) == PrefabInstanceStatus.NotAPrefab)
                return true;

            if (HasInvalidComponent(obj))
                return true;

            if (PrefabUtility.IsPrefabAssetMissing(obj))
                return true;

            return false;
        }

        private static MethodInfo s_HasInvalidComponent;
        internal static bool HasInvalidComponent(UnityEngine.Object obj)
        {
            #if UNITY
            return PrefabUtility.HasInvalidComponent(obj);
            #else
            if (s_HasInvalidComponent == null)
            {
                var type = typeof(PrefabUtility);
                s_HasInvalidComponent = type.GetMethod("s_HasInvalidComponent", BindingFlags.NonPublic | BindingFlags.Static);
                if (s_HasInvalidComponent == null)
                    return default;
            }
            return (bool)s_HasInvalidComponent.Invoke(null, new object[] { obj });
            #endif
        }

        [System.Diagnostics.Conditional("DEBUG_MISSING_SCRIPT_INDEXING")]
        static void Log(string message, Object obj = null)
        {
            Debug.Log(message, obj);
        }
    }
}
#endif
