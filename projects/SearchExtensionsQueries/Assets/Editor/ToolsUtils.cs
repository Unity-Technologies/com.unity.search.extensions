using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Search;
using System.Text;
using System.Linq;
using NUnit.Framework;

public static class ToolsUtils
{
    [MenuItem("Tools/Print selected object VISIBLE properties", priority = 11000)]
    static void PrintSelectedObjectVisibleProperties()
    {
        PrintSelectedObjectProperties(false, true);
    }

    [MenuItem("Tools/Print selected object properties", priority = 11000)]
    static void PrintSelectedObjectProperties()
    {
        PrintSelectedObjectProperties(false, false);
    }

    [MenuItem("Tools/Print selected object VISIBLE properties (children)", priority = 11000)]
    static void PrintSelectedObjectVisiblePropertiesChildren()
    {
        PrintSelectedObjectProperties(true, true);
    }

    [MenuItem("Tools/Print selected object properties (children)", priority = 11000)]
    static void PrintSelectedObjectPropertiesChildren()
    {
        PrintSelectedObjectProperties(true, false);
    }

    [MenuItem("Tools/Print Instance ID", priority = 11000)]
    static void PrintInstanceID()
    {
        if (!Selection.activeObject)
            return;
        Debug.Log(Selection.activeObject.GetInstanceID());
    }

    [MenuItem("Tools/Print GID", priority = 11000)]
    static void PrintGlobalObjectID()
    {
        if (!Selection.activeObject)
            return;
        var id = Selection.activeObject.GetInstanceID();
        var gid = GlobalObjectId.GetGlobalObjectIdSlow(id);
        Debug.Log(gid);
    }

    [MenuItem("Tools/Adb.Import", priority = 11100)]
    static void Asset()
    {
        if (!Selection.activeObject)
            return;

        var path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(path))
            return;

        AssetDatabase.ImportAsset(path);
        // AssetDatabase.ImportAsset(path, ImportAssetOptions.Default);
        // AssetDatabase.ImportAsset(path, ImportAssetOptions.DontDownloadFromCacheServer);
        // AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
    }

    static void PrintSelectedObjectProperties(bool enterChildren, bool visibleProperties)
    {
        if (!Selection.activeObject && !Selection.activeGameObject)
            return;

        PrintProperties(Selection.activeObject ?? Selection.activeGameObject, enterChildren, visibleProperties);
    }

    static void PrintProperties(UnityEngine.Object obj, bool enterChildren, bool visibleProperties)
    {
        var str = new StringBuilder();
        var so = new SerializedObject(obj);
        var path = UnityEditor.Search.SearchUtils.GetObjectPath(obj);
        
        str.AppendLine($"name: {obj.name} path: {path}");
        PrintProperties(so, str, enterChildren, visibleProperties);

        if (obj is GameObject go)
        {
            var components = go.GetComponents<Component>();
            foreach (var c in components)
            {
                so = new SerializedObject(c);
                PrintProperties(so, str, enterChildren, visibleProperties);
            }
        }

        Debug.Log(str.ToString());
    }

    static void PrintProperties(SerializedObject so, StringBuilder str, bool enterChildren, bool visibleProperties)
    {
        str.AppendLine($"Type: {so.targetObject.GetType().FullName}");
        var prop = so.GetIterator();
        bool digDeeper = true;
        while (visibleProperties ? prop.NextVisible(digDeeper) : prop.Next(digDeeper))
        {
            digDeeper = enterChildren;
            if (prop.propertyType == SerializedPropertyType.Generic)
            {
                continue;
            }
            str.AppendLine($"   {prop.propertyPath} - {prop.propertyType} - {UnityEditor.Search.SearchUtils.GetPropertyValueForQuery(prop)}");
        }
    }

    [MenuItem("Tools/Copy Queries from package")]
    static void CopyQueries()
    {
        var querySourceFolder = "../../package-queries";
        var queryDestFolder = "Assets/Queries";

        var querySourceFolderAbs = Utils.CleanPath(new FileInfo(querySourceFolder).FullName);
        Assert.IsTrue(Directory.Exists(querySourceFolderAbs), $"Directory: {querySourceFolderAbs} doesn't exists");
        var queryDestFolderAbs = Utils.CleanPath(new FileInfo(queryDestFolder).FullName);

        if (!Directory.Exists(queryDestFolderAbs))
        {
            Directory.CreateDirectory(queryDestFolderAbs);
        }

        var queryFiles = Directory.EnumerateFiles(querySourceFolderAbs, "*.asset", SearchOption.AllDirectories);
        foreach (var f in queryFiles)
        {
            var queryFile = Utils.CleanPath(f);
            var queryLocalPath = queryFile.Replace(querySourceFolderAbs, "");
            var queryResourcePath = $"{queryDestFolderAbs}{queryLocalPath}";
            var queryResourceFolder = Utils.CleanPath(Path.GetDirectoryName(queryResourcePath));
            if (!Directory.Exists(queryResourceFolder))
            {
                Directory.CreateDirectory(queryResourceFolder);
            }

            File.Copy(queryFile, queryResourcePath, true);
            File.Copy($"{queryFile}.meta", $"{queryResourcePath}.meta", true);
            Debug.Log($"Copied {queryFile} to {queryResourcePath}");
        }
    }
}
