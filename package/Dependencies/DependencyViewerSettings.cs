using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Search;
using UnityEngine;

[Serializable]
public class DependencyViewerSettings
{
    private static string s_SettingsPath = "UserSettings/DepViewer.settings";

    public List<string> ignoredResultExtensions = new List<string>();
    public int dependencyDepthLevel = 1;
    public bool showSceneRefs = true;
    public DependencyState.Columns visibleColumns = DependencyState.Columns.Default;
    public string currentStateProviderName = null;

    public bool IsIgnoredResultPath(string path)
    {
        foreach (var ext in ignoredResultExtensions)
        {
            if (path.EndsWith(ext))
            {
                return true;
            }
        }
        return false;
    }

    private static DependencyViewerSettings g_Instance;
    public static DependencyViewerSettings Get()
    {
        if (g_Instance == null)
        {
            try
            {
                if (File.Exists(s_SettingsPath))
                {
                    g_Instance = JsonUtility.FromJson<DependencyViewerSettings>(File.ReadAllText(s_SettingsPath));
                }
            }
            catch
            {
            }

            if (g_Instance == null)
            {
                g_Instance = CreateDefaultSettings();
                g_Instance.Save();
            }
        }

        return g_Instance;
    }

    public static DependencyViewerSettings CreateDefaultSettings()
    {
        var settings = new DependencyViewerSettings();
        settings.ignoredResultExtensions = new List<string>();
        return settings;
    }

    public void Save()
    {
        var content = JsonUtility.ToJson(this, true);
        Debug.Log($"Save Settings: {content}");
        File.WriteAllText(s_SettingsPath, content);
    }
}
