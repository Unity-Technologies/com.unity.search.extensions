#if UNITY_2021_2_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.Search.Collections
{
    static class SearchCollectionExtensions
    {
        [SearchActionsProvider]
        internal static IEnumerable<SearchAction> ActionHandlers()
        {
            #if UNITY_2022_1_OR_NEWER
            yield return new SearchAction("scene", "isolate", EditorGUIUtility.TrIconContent("SceneViewFx").image as Texture2D, "Isolate selected object(s)", IsolateObjects);
            #endif
            yield return new SearchAction("scene", "lock", EditorGUIUtility.TrIconContent("Locked").image as Texture2D, "Lock selected object(s)", LockObjects);
        }

        private static void LockObjects(SearchItem[] items)
        {
            var svm = SceneVisibilityManager.instance;
            var objects = items.Select(e => e.ToObject<GameObject>()).Where(g => g).ToArray();
            if (objects.Length == 0)
                return;
            if (svm.IsPickingDisabled(objects[0]))
                svm.EnablePicking(objects, includeDescendants: true);
            else
                svm.DisablePicking(objects, includeDescendants: true);
        }

        private static void IsolateObjects(SearchItem[] items)
        {
            var svm = SceneVisibilityManager.instance;
            if (svm.IsCurrentStageIsolated())
                svm.ExitIsolation();
            else
                svm.Isolate(items.Select(e => e.ToObject<GameObject>()).Where(g=>g).ToArray(), includeDescendants: true);
        }

    }
}
#endif
