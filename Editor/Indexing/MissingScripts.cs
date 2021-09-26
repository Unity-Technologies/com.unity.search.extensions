#define DEBUG_MISSING_SCRIPT_INDEXING
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityEditor.Search
{
    static class MissingScripts
    {
        #if DEBUG_MISSING_SCRIPT_INDEXING
        [MenuItem("Assets/Index Missing Scripts")]
        internal static void IndexMissingScriptSelectedAsset()
        {
            if (Selection.assetGUIDs.Length == 0)
                return;
            IndexSceneMissingScripts(AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]));
        }
        #endif

        [CustomObjectIndexer(typeof(SceneAsset), version = 15)]
        internal static void IndexMissingScriptAssets(CustomObjectIndexerTarget context, ObjectIndexer indexer)
        {
            IndexSceneMissingScripts(context.id, context.documentIndex, indexer);
        }

        static void IndexSceneMissingScripts(in string assetPath, int documentIndex = -1, ObjectIndexer indexer = null)
        {
            var scene = EditorSceneManager.OpenScene(assetPath, OpenSceneMode.Additive);
            while (!scene.isLoaded)
                ;
            IndexSceneMissingScripts(scene, documentIndex, indexer);
            EditorSceneManager.CloseScene(scene, removeScene: true);
        }

        static void IndexSceneMissingScripts(in UnityEngine.SceneManagement.Scene scene, int documentIndex = -1, ObjectIndexer indexer = null)
        {
            var gameObjects = SearchUtils.FetchGameObjects(scene).ToList();
            Log($"Indexing missing scripts for {scene.path} containing {gameObjects.Count} objects");
        
            foreach (var go in gameObjects)
            {
                if (!CanHaveMissingScripts(go))
                    continue;

                IndexSceneMissingScripts(go, documentIndex, indexer);
            }
        }

        static void IndexSceneMissingScripts(GameObject go, int documentIndex = -1, ObjectIndexer indexer = null)
        {
            int goDocumentIndex = -1;
            bool hasMissingScripts = false;

            if (PrefabUtility.IsPartOfPrefabAsset(go))
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

//                 Debug.Log($"{go} > IsPartOfPrefabAsset={PrefabUtility.IsPartOfPrefabAsset(go)}");
//                 if (PrefabUtility.IsPartOfPrefabAsset(go))
//                     Debug.Log($"{go} > HasManagedReferencesWithMissingTypes={PrefabUtility.HasManagedReferencesWithMissingTypes(go)}");
//                 Debug.Log($"{go} > IsPartOfPrefabInstance={PrefabUtility.IsPartOfPrefabInstance(go)}");
//                 Debug.Log($"{go} > IsPartOfNonAssetPrefabInstance={PrefabUtility.IsPartOfNonAssetPrefabInstance(go)}");
//                 Debug.Log($"{go} > IsAnyPrefabInstanceRoot={PrefabUtility.IsAnyPrefabInstanceRoot(go)}");
//                 Debug.Log($"{go} > HasPrefabInstanceAnyOverrides={PrefabUtility.HasPrefabInstanceAnyOverrides(go, false)}", go);
//                 Debug.Log($"{go} > IsDisconnectedFromPrefabAsset={PrefabUtility.IsDisconnectedFromPrefabAsset(go)}");
//                 Debug.Log($"{go} > IsPartOfPrefabThatCanBeAppliedTo={PrefabUtility.IsPartOfPrefabThatCanBeAppliedTo(go)}", go);

                hasMissingScripts = true;
                if (indexer != null)
                {
                    if (GetBrokenObjectDocumentIndex(ref goDocumentIndex, go, indexer))
                        indexer.IndexProperty(goDocumentIndex, "missing", "scripts", saveKeyword: true, exact: true);
                }
            }

            if (PrefabUtility.IsPrefabAssetMissing(go))
            {
                hasMissingScripts = true;
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
            goDocumentIndex = indexer.AddDocument(gid.ToString(), SearchUtils.GetObjectPath(go), go.scene.path,
                checkIfExists: true, SearchDocumentFlags.Nested | SearchDocumentFlags.Object);
            indexer.IndexProperty(goDocumentIndex, "is", "broken", saveKeyword: true, exact: true);
            return true;
        }

        static bool CanHaveMissingScripts(Object obj)
        {
            if (PrefabUtility.GetPrefabInstanceStatus(obj) == PrefabInstanceStatus.NotAPrefab)
                return true;

            if (PrefabUtility.HasInvalidComponent(obj))
                return true;

            if (PrefabUtility.IsPrefabAssetMissing(obj))
                return true;

            return false;
        }

        [System.Diagnostics.Conditional("DEBUG_MISSING_SCRIPT_INDEXING")]
        static void Log(string message, Object obj = null)
        {
            Debug.Log(message, obj);
        }
    }
}
