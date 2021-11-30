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
            yield return new SearchAction("scene", "isolate", Utils.LoadIcon("Isolate"), "Isolate selected object(s)", IsolateObjects);
            yield return new SearchAction("scene", "lock", Utils.LoadIcon("Locked"), "Lock selected object(s)", LockObjects);
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
