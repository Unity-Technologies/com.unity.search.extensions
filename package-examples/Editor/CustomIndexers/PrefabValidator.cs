using UnityEditor;
using UnityEditor.Search;
using UnityEngine;

public class PrefabValidator
{
    // More info on this Attribute: https://docs.unity3d.com/ScriptReference/Search.CustomObjectIndexerAttribute.html
    // Article about Custom Asset Indexing: https://github.com/Unity-Technologies/com.unity.search.extensions/wiki/Custom-Asset-Indexing
    [CustomObjectIndexer(typeof(GameObject), version = 1)]
    internal static void PrefabWithAudioListener(CustomObjectIndexerTarget context, ObjectIndexer indexer)
    {
        /*
        This customindexer will tag all prefab who contains an AudioListener or a Children with an AudioListener.

        running the query: `p: validate:PrefabWithAudioListener` will yields all of these prefabs.

        */
        var go = context.target as GameObject;
        if (go == null)
            return;

        if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(go)))
            return;

        var hasAudioListener = go.GetComponentInChildren<AudioListener>();
        if (hasAudioListener == null)
            return;

        // NOTE: Always use IndexWord or IndexProperty instead of AddWord, AddProperty since it correctly handles case sensitivity.
        // Note: Sometimes changing code in a CustomObjectIndexer won't reindexed the relevant objects. You might have to completely reindex your project
        // or to reimport the prefabs.
        indexer.IndexProperty(context.documentIndex, "validate", "PrefabWithAudioListener", true);
    }
}
