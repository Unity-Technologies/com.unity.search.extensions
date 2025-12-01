using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Search;
using NUnit.Framework;

public static class ToolsUtils
{
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
