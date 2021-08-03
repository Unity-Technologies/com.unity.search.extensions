//#define DEBUG_INDEXING
using System;
using System.IO;
using System.Linq;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace UnityEditor.Search
{
    readonly struct TextureAsset
    {
        public readonly string path;
        public readonly Texture2D texture;

        public bool valid => texture != null;

        public TextureAsset(string path)
        {
            this.path = path;
            this.texture = AssetDatabase.LoadMainAssetAtPath(path) as Texture2D;
        }

        public TextureAsset(string path, Texture2D texture)
        {
            this.path = path;
            this.texture = texture;
        }
    }
    [ExcludeFromPreset, ScriptedImporter(ImageDatabase.version, importQueueOffset: int.MaxValue, ext: "idb")]
    class ImageDatabaseImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var filePath = ctx.assetPath;
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            hideFlags |= HideFlags.HideInInspector;

            #if DEBUG_INDEXING
            using (new DebugTimer($"Importing image index {fileName}"))
            #endif
            {
                var db = ScriptableObject.CreateInstance<ImageDatabase>();
                db.name = fileName;
                db.hideFlags = HideFlags.NotEditable;

                BuildIndex(db);

                ctx.AddObjectToAsset("idb", db);
                ctx.SetMainObject(db);
            }
        }

        static void BuildIndex(ImageDatabase idb)
        {
            try
            {
                var assetPaths = AssetDatabase.FindAssets("t:texture2d").Select(AssetDatabase.GUIDToAssetPath);
                var textures = assetPaths.Select(path => new TextureAsset(path)).Where(t => t.valid);

                var current = 1;
                var total = textures.Count();
                foreach (var textureAsset in textures)
                {
                    ReportProgress(textureAsset.texture.name, current / (float)total, false, idb);
                    idb.IndexTexture(textureAsset.path, textureAsset.texture);
                    ++current;
                }
                idb.WriteBytes();

                ReportProgress("Indexing Finished", 1.0f, true, idb);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                ReportProgress("Indexing failed", 1.0f, true, idb);
            }
        }

        static void ReportProgress(string description, float progress, bool finished, ImageDatabase idb)
        {
            EditorUtility.DisplayProgressBar($"Building {idb.name} index...", description, progress);
            if (finished)
                EditorUtility.ClearProgressBar();
        }

        public static void CreateIndex(string path)
        {
            var dirPath = path;

            var indexPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(dirPath, $"{Path.GetFileNameWithoutExtension(path)}.idb")).Replace("\\", "/");
            File.WriteAllText(indexPath, "");
            AssetDatabase.ImportAsset(indexPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, $"Generated image index at {indexPath}");
        }

        [MenuItem("Assets/Create/Search/Image Index")]
        internal static void CreateIndexProject()
        {
            CreateIndex(GetSelectionFolderPath());
        }

        private static string GetSelectionFolderPath()
        {
            var folderPath = "Assets";
            if (Selection.activeObject != null)
                folderPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (File.Exists(folderPath))
                folderPath = Path.GetDirectoryName(folderPath);
            return folderPath;
        }
    }
}
