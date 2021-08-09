#if USE_SEARCH_TABLE
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace UnityEditor.Search
{
    static class DependencyExtensions
    {
        static class Styles
        {
            public static GUIStyle dirPath;
            public static GUIStyle filename;

            public static Texture2D sceneIcon = Utils.LoadIcon("SceneAsset Icon");
            public static Texture2D favoriteIcon = Utils.LoadIcon("Favorite Icon");
        }

        [MenuItem("Window/Search/Dependency Viewer", priority = 5679)]
        internal static void OpenNew()
        {
            var win = EditorWindow.CreateWindow<DependencyViewer>();
            win.position = Utils.GetMainWindowCenteredPosition(new Vector2(1000, 400));
            win.Show();
        }

        [SearchExpressionEvaluator]
        internal static IEnumerable<SearchItem> Selection(SearchExpressionContext c)
        {
            return TaskEvaluatorManager.EvaluateMainThread<SearchItem>(CreateItemsFromSelection);
        }

        [SearchExpressionEvaluator("deps", SearchExpressionType.Iterable, SearchExpressionType.Boolean | SearchExpressionType.Optional)]
        internal static IEnumerable<SearchItem> SceneUses(SearchExpressionContext c)
        {
            var args = c.args[0].Execute(c);
            var fetchSceneRefs = c.args.Length < 2 || c.args[1].GetBooleanValue(true);
            var depProvider = SearchService.GetProvider(Dependency.providerId);
            var sceneProvider = fetchSceneRefs ? SearchService.GetProvider(Providers.BuiltInSceneObjectsProvider.type) : null;
            foreach (var e in args)
            {
                if (e == null || e.value == null)
                {
                    yield return null;
                    continue;
                }

                var id = e.value.ToString();
                if (Utils.TryParse(id, out int instanceId))
                {
                    foreach (var item in TaskEvaluatorManager.EvaluateMainThread(() =>
                        GetSceneObjectDependencies(c.search, sceneProvider, depProvider, instanceId).ToList()))
                    {
                        yield return item;
                    }
                }
            }
        }

        static IEnumerable<SearchItem> GetSceneObjectDependencies(SearchContext context, SearchProvider sceneProvider, SearchProvider depProvider, int instanceId)
        {
            var obj = EditorUtility.InstanceIDToObject(instanceId);
            if (!obj)
                yield break;

            var go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (!go && obj is Component goc)
            {
                foreach (var ce in GetComponentDependencies(context, sceneProvider, depProvider, goc))
                    yield return ce;
            }
            else if (go)
            {
                // Index any prefab reference
                var containerPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                if (!string.IsNullOrEmpty(containerPath))
                    yield return depProvider.CreateItem(context, AssetDatabase.AssetPathToGUID(containerPath));

                var gocs = go.GetComponents<Component>();
                for (int componentIndex = 1; componentIndex < gocs.Length; ++componentIndex)
                {
                    var c = gocs[componentIndex];
                    if (!c || (c.hideFlags & HideFlags.HideInInspector) == HideFlags.HideInInspector)
                        continue;

                    foreach (var ce in GetComponentDependencies(context, sceneProvider, depProvider, c))
                        yield return ce;
                }
            }
        }

        static bool IsGUIDLike(string str)
        {
            return str != null && str.Length == 32;
        }

        static IEnumerable<SearchItem> GetComponentDependencies(SearchContext context, SearchProvider sceneProvider, SearchProvider depProvider, Component c)
        {
            using (var so = new SerializedObject(c))
            {
                var p = so.GetIterator();
                var next = p.NextVisible(true);
                while (next)
                {
                    if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue)
                    {
                        var assetPath = AssetDatabase.GetAssetPath(p.objectReferenceValue);
                        if (!string.IsNullOrEmpty(assetPath))
                        {
                            yield return depProvider.CreateItem(context, AssetDatabase.AssetPathToGUID(assetPath), assetPath, null, null, null);
                        }
                        else if (sceneProvider != null)
                        {
                            if (p.objectReferenceValue is GameObject cgo)
                                yield return CreateSceneItem(context, sceneProvider, cgo);
                            else if (p.objectReferenceValue is Component cc && cc.gameObject)
                                yield return CreateSceneItem(context, sceneProvider, cc.gameObject);
                        }
                    }
                    // This handles any string that is GUID like.
                    else if (p.propertyType == SerializedPropertyType.String && IsGUIDLike(p.stringValue))
                    {
                        var guid = p.stringValue;
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!string.IsNullOrEmpty(path))
                            yield return depProvider.CreateItem(context, guid, path, null, null, null);
                    }
                    next = p.NextVisible(p.hasVisibleChildren);
                }
            }
        }

        static SearchItem CreateSceneItem(SearchContext context, SearchProvider sceneProvider, GameObject go)
        {
            return Providers.SceneProvider.AddResult(context, sceneProvider, go);
        }

        static void CreateItemsFromSelection(Action<SearchItem> yielder)
        {
            foreach (var obj in UnityEditor.Selection.objects)
            {
                var assetPath = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(assetPath))
                    yielder(EvaluatorUtils.CreateItem(assetPath));
                else
                    yielder(EvaluatorUtils.CreateItem(obj.GetInstanceID()));
            }
        }

        [SearchColumnProvider("path")]
        internal static void InitializePathColumn(SearchColumn column)
        {
            column.drawer = DrawPath;
        }

        private static object DrawPath(SearchColumnEventArgs args)
        {
            if (Event.current.type != EventType.Repaint || !(args.value is string path) || string.IsNullOrEmpty(path))
                return args.value;

            path = path.Trim('/', '\\');

            var rect = args.rect;
            var dirName = System.IO.Path.GetDirectoryName(path);
            var thumbnail = args.item.GetThumbnail(args.item.context ?? args.context);

            if (Styles.filename == null)
            {
                var itemStyle = ItemSelectors.GetItemContentStyle(args.column);
                Styles.filename = new GUIStyle(itemStyle) { padding = new RectOffset(0, 0, 0, 0) };
            }
            if (Styles.dirPath == null)
            {
                Styles.dirPath = new GUIStyle(Styles.filename) { fontSize = Styles.filename.fontSize - 3, padding = new RectOffset(2, 0, 0, 0) };
            }

            Texture2D badge = null;
            const float badgeSize = 18f;
            if (SearchSettings.searchItemFavorites.Contains(args.item.id))
                badge = Styles.favoriteIcon;
            else if (string.Equals(args.item.provider.id, "scene", StringComparison.Ordinal))
                badge = Styles.sceneIcon;

            if (badge)
                rect.xMax -= badgeSize;

            if (string.IsNullOrEmpty(dirName))
            {
                var filenameContent = Utils.GUIContentTemp(path, thumbnail);
                Styles.filename.Draw(rect, filenameContent, false, false, false, false);
            }
            else
            {
                var dir = Utils.GUIContentTemp(dirName.Replace("\\", "/") + "/", thumbnail);
                var dirPathWidth = Styles.dirPath.CalcSize(dir).x;
                var oldColor = GUI.color;
                GUI.color = new Color(oldColor.r, oldColor.g, oldColor.b, oldColor.a * 0.8f);
                Styles.dirPath.Draw(rect, dir, false, false, false, false);
                rect.xMin += dirPathWidth;
                GUI.color = oldColor;

                var filename = System.IO.Path.GetFileName(path);
                Styles.filename.Draw(rect, filename, false, false, false, false);
            }

            if (badge)
            { 
                var badgeRect = new Rect(args.rect.xMax - badgeSize + 2f, rect.yMin, badgeSize, badgeSize);
                GUI.DrawTexture(badgeRect, badge, ScaleMode.ScaleToFit);
            }

            return args.value;
        }
    }
}
#endif
