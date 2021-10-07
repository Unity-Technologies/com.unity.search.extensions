using System.Collections;
using System.IO;
using UnityEditor;

namespace Unity.PerformanceTracking.Tests
{
    internal static class TestUtils
    {
        public const string testGeneratedFolder = "Assets/TempTestGeneratedData/";

        public static void DeleteFolder(string path)
        {
            Directory.Delete(path, true);
            if (path.EndsWith("/"))
            {
                path = path.Remove(path.Length - 1);
            }
            File.Delete(path + ".meta");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        public static void CreateFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }
        }

        public static IEnumerator WaitForDelayCall()
        {
            var actionCalled = false;
            EditorApplication.delayCall += () =>
            {
                actionCalled = true;
            };
            while (!actionCalled)
                yield return null;
        }

        public static IEnumerator WaitForTime(double seconds)
        {
            var currentTime = EditorApplication.timeSinceStartup;
            while (currentTime + seconds >= EditorApplication.timeSinceStartup)
            {
                yield return null;
            }
        }
    }
}
