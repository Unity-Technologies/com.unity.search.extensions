using System;
using UnityEngine;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("com.unity.search.extensions.tests")]

namespace UnityEditor.Search
{
    // Helper Rect extension methods
    static class RectExtensions
    {
        public static Vector2 TopLeft(this Rect rect)
        {
            return new Vector2(rect.xMin, rect.yMin);
        }
        public static Rect ScaleSizeBy(this Rect rect, float scale)
        {
            return rect.ScaleSizeBy(scale, rect.center);
        }
        public static Rect ScaleSizeBy(this Rect rect, float scale, Vector2 pivotPoint)
        {
            Rect result = rect;
            result.x -= pivotPoint.x;
            result.y -= pivotPoint.y;
            result.xMin *= scale;
            result.xMax *= scale;
            result.yMin *= scale;
            result.yMax *= scale;
            result.x += pivotPoint.x;
            result.y += pivotPoint.y;
            return result;
        }
        public static Rect ScaleSizeBy(this Rect rect, Vector2 scale)
        {
            return rect.ScaleSizeBy(scale, rect.center);
        }
        public static Rect ScaleSizeBy(this Rect rect, Vector2 scale, Vector2 pivotPoint)
        {
            Rect result = rect;
            result.x -= pivotPoint.x;
            result.y -= pivotPoint.y;
            result.xMin *= scale.x;
            result.xMax *= scale.x;
            result.yMin *= scale.y;
            result.yMax *= scale.y;
            result.x += pivotPoint.x;
            result.y += pivotPoint.y;
            return result;
        }

        public static Rect OffsetBy(this Rect rect, Vector2 offset)
        {
            return new Rect(rect.position + offset, rect.size);
        }

        public static Rect PadBy(this Rect rect, float padding)
        {
            return rect.PadBy(new Vector4(padding, padding, padding, padding));
        }

        public static Rect PadBy(this Rect rect, Vector4 padding)
        {
            return new Rect(rect.x + padding.x, rect.y + padding.y, rect.width - padding.x - padding.z, rect.height - padding.y - padding.w);
        }

        public static bool HorizontalOverlaps(this Rect rect, Rect other)
        {
            return other.xMax > rect.xMin && other.xMin < rect.xMax;
        }

        public static bool VerticalOverlaps(this Rect rect, Rect other)
        {
            return other.yMax > rect.yMin && other.yMin < rect.yMax;
        }
    }

    class EditorZoomArea
    {
        private static Matrix4x4 _prevGuiMatrix;
        private static Rect s_WorldBoundRect;

        public static Rect Begin(in float zoomScale, in Rect screenCoordsArea, in Rect worldBoundRect)
        {
            s_WorldBoundRect = worldBoundRect;

            // End the group Unity begins automatically for an EditorWindow to clip out the window tab.
            // This allows us to draw outside of the size of the EditorWindow.
            GUI.EndGroup();

            Rect clippedArea = screenCoordsArea.ScaleSizeBy(1.0f / zoomScale, screenCoordsArea.TopLeft());
            clippedArea.x += worldBoundRect.x;
            clippedArea.y += worldBoundRect.y;
            GUI.BeginGroup(clippedArea);

            _prevGuiMatrix = GUI.matrix;
            Matrix4x4 translation = Matrix4x4.TRS(clippedArea.TopLeft(), Quaternion.identity, Vector3.one);
            Matrix4x4 scale = Matrix4x4.Scale(new Vector3(zoomScale, zoomScale, 1.0f));
            GUI.matrix = translation * scale * translation.inverse * GUI.matrix;

            return clippedArea;
        }

        public static void End()
        {
            GUI.matrix = _prevGuiMatrix;
            GUI.EndGroup();
            GUI.BeginGroup(s_WorldBoundRect);
        }
    }

    static class DependencyUtils
    { 
        public static string FormatCount(ulong count)
        {
            #if !USE_SEARCH_EXTENSION_API
            return Utils.FormatCount(count);
            #else
            return SearchUtils.FormatCount(count);
            #endif
        }

        public static int GetMainAssetInstanceID(string path)
        {
            #if !USE_SEARCH_EXTENSION_API
            return Utils.GetMainAssetInstanceID(path);
            #else
            return SearchUtils.GetMainAssetInstanceID(path);
            #endif
        }

        public static bool TryParse<T>(string expression, out T result)
        {
            #if !USE_SEARCH_EXTENSION_API
            return Utils.TryParse(expression, out result);
            #else
            return SearchUtils.TryParse(expression, out result);
            #endif
        }

        public static void PingAsset(string path)
        {
            #if !USE_SEARCH_EXTENSION_API
            Utils.PingAsset(path);
            #else
            SearchUtils.PingAsset(path);
            #endif
        }

        public static void StartDrag(UnityEngine.Object[] objects, string[] paths, string label)
        {
            #if !USE_SEARCH_EXTENSION_API
            Utils.StartDrag(objects, paths, label);
            #else
            SearchUtils.StartDrag(objects, paths, label);
            #endif
        }

        public static Rect GetMainWindowCenteredPosition(Vector2 size)
        {
            #if !USE_SEARCH_EXTENSION_API
            return Utils.GetMainWindowCenteredPosition(size);
            #else
            return SearchUtils.GetMainWindowCenteredPosition(size);
            #endif
        }
    }
}
