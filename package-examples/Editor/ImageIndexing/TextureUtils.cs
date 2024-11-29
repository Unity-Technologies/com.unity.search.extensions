using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace UnityEditor.Search
{
    static class TextureUtils
    {
        public static Texture2D CopyTextureReadable(Texture2D texture, int width, int height)
        {
            var savedRT = RenderTexture.active;
            var savedViewport = GetRawViewportRect();

            var tmp = RenderTexture.GetTemporary(
                width, height,
                0,
                SystemInfo.GetGraphicsFormat(DefaultFormat.LDR));
            var mat = GetMaterialForSpecialTexture(texture, null, QualitySettings.activeColorSpace == ColorSpace.Linear);
            if (mat != null)
                Graphics.Blit(texture, tmp, mat);
            else
                Graphics.Blit(texture, tmp);

            RenderTexture.active = tmp;
            var uncompressedTexture = new Texture2D(width, height, HasAlphaTextureFormat(texture) ? TextureFormat.ARGB32 : TextureFormat.RGB24, false);
            uncompressedTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            uncompressedTexture.Apply();
            RenderTexture.ReleaseTemporary(tmp);

            SetRenderTextureNoViewport(savedRT);
            SetRawViewportRect(savedViewport);

            return uncompressedTexture;
        }

        public static Color32[] GetPixels32(Texture2D texture)
        {
            Color32[] pixels;
            if (texture.isReadable)
                pixels = texture.GetPixels32();
            else
            {
                var copy = TextureUtils.CopyTextureReadable(texture, texture.width, texture.height);
                pixels = copy.GetPixels32();
            }

            return pixels;
        }

        public static Color[] GetPixels(Texture2D texture)
        {
            Color[] pixels;
            if (texture.isReadable)
                pixels = texture.GetPixels();
            else
            {
                var copy = TextureUtils.CopyTextureReadable(texture, texture.width, texture.height);
                pixels = copy.GetPixels();
            }

            return pixels;
        }

        public static Texture2D ToTexture(Color32[] pixels, int width, int height)
        {
            var t = new Texture2D(width, height, TextureFormat.RGB24, false);
            t.SetPixels32(pixels);
            t.Apply();
            return t;
        }

        public static Texture2D ToTexture(Color[] pixels, int width, int height)
        {
            var t = new Texture2D(width, height, TextureFormat.RGB24, false);
            t.SetPixels(pixels);
            t.Apply();
            return t;
        }

        private static PropertyInfo s_RawViewportRect;
        public static Rect GetRawViewportRect()
        {
            if (s_RawViewportRect == null)
            {
                var t = typeof(ShaderUtil);
                s_RawViewportRect = t.GetProperty("rawViewportRect", BindingFlags.NonPublic | BindingFlags.Static);
            }

            return (Rect)s_RawViewportRect.GetValue(null);
        }

        public static void SetRawViewportRect(Rect rect)
        {
            if (s_RawViewportRect == null)
            {
                var t = typeof(ShaderUtil);
                s_RawViewportRect = t.GetProperty("rawViewportRect", BindingFlags.NonPublic | BindingFlags.Static);
            }

            s_RawViewportRect.SetValue(null, rect);
        }

        private static MethodInfo s_SetRenderTextureNoViewport;
        public static void SetRenderTextureNoViewport(RenderTexture rt)
        {
            if (s_SetRenderTextureNoViewport == null)
            {
                var t = typeof(EditorGUIUtility);
                s_SetRenderTextureNoViewport = t.GetMethod("SetRenderTextureNoViewport", BindingFlags.NonPublic | BindingFlags.Static);
            }

            s_SetRenderTextureNoViewport.Invoke(null, new[] { rt });
        }

        private static MethodInfo s_GetMaterialForSpecialTexture;
        public static Material GetMaterialForSpecialTexture(Texture2D source, Material defaultMaterial, bool normals2Linear, bool useVTMaterialWhenPossible = true)
        {
            if (s_GetMaterialForSpecialTexture == null)
            {
                var t = typeof(EditorGUI);
                s_GetMaterialForSpecialTexture = t.GetMethod("GetMaterialForSpecialTexture", BindingFlags.NonPublic | BindingFlags.Static);
            }

            return (Material)s_GetMaterialForSpecialTexture.Invoke(null, new object[] { source, defaultMaterial, normals2Linear, useVTMaterialWhenPossible });
        }

        static MethodInfo s_HasAlphaTextureFormat;

        public static bool HasAlphaTextureFormat(Texture2D texture)
        {
            if (s_HasAlphaTextureFormat == null)
            {
                Assembly assembly = typeof(UnityEditor.SerializedProperty).Assembly;
                var type = assembly.GetTypes().First(t => t.FullName == "UnityEditor.TextureUtil");
                s_HasAlphaTextureFormat = type.GetMethod("HasAlphaTextureFormat", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);
                if (s_HasAlphaTextureFormat == null)
                    return false;
            }

            return (bool)s_HasAlphaTextureFormat.Invoke(null, new object[] { texture.format });
        }
    }
}
