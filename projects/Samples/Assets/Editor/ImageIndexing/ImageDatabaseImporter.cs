//#define DEBUG_INDEXING
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace UnityEditor.Search
{
    [ExcludeFromPreset, ScriptedImporter(ImageDatabase.version, importQueueOffset: int.MaxValue, ext: "idb")]
    class ImageDatabaseImporter : ScriptedImporter
    {
        static List<SupportedImageType> s_SupportedImageTypes = new List<SupportedImageType>()
        {
            new SupportedImageType
            {
                assetType = typeof(Texture2D),
                imageType = ImageType.Texture2D,
                assetDatabaseQuery = "t:texture2d",
                textureAssetCreator = (assetPath) => new TextureAsset(assetPath)
            },
            new SupportedImageType
            {
                assetType = typeof(Sprite),
                imageType = ImageType.Sprite,
                assetDatabaseQuery = "t:sprite",
                textureAssetCreator = (assetPath) => new SpriteAsset(assetPath)
            }
        };

        public static IEnumerable<SupportedImageType> SupportedImageTypes => s_SupportedImageTypes;

        public static bool IsSupportedType(Type type)
        {
            return s_SupportedImageTypes.Any(sit => sit.assetType == type);
        }

        public static bool IsSupportedImageType(ImageType imageType)
        {
            return s_SupportedImageTypes.Any(sit => sit.imageType == imageType);
        }

        public static Type GetTypeFromImageType(ImageType imageType)
        {
            var sit = s_SupportedImageTypes.FirstOrDefault(sit => sit.imageType == imageType);
            if (sit.imageType == ImageType.None)
                return null;
            return sit.assetType;
        }

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
                var allAssets = new List<ITextureAsset>();
                foreach (var supportedImageType in s_SupportedImageTypes)
                {
                    var assetPaths = AssetDatabase.FindAssets(supportedImageType.assetDatabaseQuery).Select(AssetDatabase.GUIDToAssetPath);
                    var assets = assetPaths.Select(path => supportedImageType.textureAssetCreator(path)).Where(t => t.valid);
                    allAssets.AddRange(assets);
                }

                var comparer = new TextureAssetComparer();
                allAssets = allAssets.Distinct(comparer).ToList();

                var current = 1;
                var total = allAssets.Count;
                foreach (var textureAsset in allAssets)
                {
                    ReportProgress(textureAsset.texture.name, current / (float)total, false, idb);
                    idb.IndexTexture(StringUtils.SanitizePath(textureAsset.path), textureAsset.imageType, textureAsset.texture);
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
