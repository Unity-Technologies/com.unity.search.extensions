using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;

static class EasySearchProviderExample
{
    enum ExampleProvider { win, material, shader, folder, component, project, res }

    /// <summary>
    /// Search opened editor windows
    /// </summary>
    [SearchItemProvider]
    public static SearchProvider ExampleWindows()
    {
        return EasySearchProvider.Create(ExampleProvider.win.ToString(), _ => Resources.FindObjectsOfTypeAll<EditorWindow>())
            .AddAction("select", win => win.Focus())
            .AddFilter("floating", "Floating", win => !win.docked)
            .AddOption(ShowDetailsOptions.Inspector)
            .AddOption(EasyOptions.DisplayFilterValueInDescription)
            .AddByReflectionActions();
    }

    /// <summary>
    /// Search any loaded material and select them in the inspector.
    /// </summary>
    [SearchItemProvider]
    public static SearchProvider ExampleMaterials()
    {
        return EasySearchProvider.Create(_ => Resources.FindObjectsOfTypeAll<Material>())
            .AddAction("select", o => Selection.activeObject = o)
            .AddOption(ShowDetailsOptions.Actions | ShowDetailsOptions.Inspector)
            .RemoveOption(ShowDetailsOptions.Description);
    }

    /// <summary>
    /// Search any loaded shader.
    /// Select them in the inspector.
    /// View the shader source in the preview inspector.
    /// </summary>
    [SearchItemProvider]
    public static SearchProvider ExampleShaders()
    {
        static string ReadSource(Shader shader)
        {
            var shaderPath = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrEmpty(shaderPath))
                return ShaderUtil.GetShaderData(shader).ToString();
            if (System.IO.File.Exists(shaderPath))
                return $"<size=8>{System.IO.File.ReadAllText(shaderPath)}</size>";
            return shaderPath;
        }

        static string FetchShaderSource(Shader shader, SearchItemOptions options)
        {
            if ((options & SearchItemOptions.FullDescription) != 0)
                return ReadSource(shader) ?? shader.ToString();
            return AssetDatabase.GetAssetPath(shader);
        }

        return EasySearchProvider.Create(_ => Resources.FindObjectsOfTypeAll<Shader>())
            .AddAction("select", o => Selection.activeObject = o)
            .AddOption(ShowDetailsOptions.Actions | ShowDetailsOptions.Inspector | ShowDetailsOptions.Description)
            .AddFilter("source", "Source Code", s => ReadSource(s))
            .SetDescriptionHandler(FetchShaderSource);
    }

    /// <summary>
    /// Search any active game object component.
    /// </summary>
    [SearchItemProvider]
    public static SearchProvider ExampleComponents()
    {
        return EasySearchProvider.Create(_ => Resources.FindObjectsOfTypeAll<Component>())
            .SetDescriptionHandler((obj, options) => obj.GetType().FullName)
            .AddAction("ping", o => EditorGUIUtility.PingObject(o.gameObject))
            .AddAction("select", o => Selection.activeObject = o)
            .AddOption(ShowDetailsOptions.Actions | ShowDetailsOptions.Inspector)
            .RemoveOption(ShowDetailsOptions.Description);
    }

    /// <summary>
    /// Search all project folders.
    /// </summary>
    /// <returns></returns>
    [SearchItemProvider]
    public static SearchProvider ExampleFolders()
    {
        var folderIcon = EditorGUIUtility.FindTexture("Folder Icon");
        return EasySearchProvider.Create(ExampleProvider.folder.ToString(), "Folders",
            _ => System.IO.Directory.EnumerateDirectories("Assets", "*", System.IO.SearchOption.AllDirectories).Select(d => d.Replace("\\", "/")))
            .SetThumbnailHandler(dir => folderIcon)
            .AddAction("open", dir => EditorUtility.RevealInFinder(dir))
            .AddAction("select", dir => Selection.activeObject =AssetDatabase.LoadMainAssetAtPath(dir))
            .AddOption(EasyOptions.DescriptionSameAsLabel | EasyOptions.SortByName);
    }

    /// <summary>
    /// Search editor bundle resources.
    /// Note: that bundle resources are cached once.
    /// </summary>
    /// <returns></returns>
    [SearchItemProvider]
    public static SearchProvider ExampleEditorBundles()
    {
        Func<UnityEngine.Object, bool> FR = r => string.Equals(AssetDatabase.GetAssetPath(r), "Library/unity editor resources", StringComparison.Ordinal);
        return EasySearchProvider.Create(ExampleProvider.res.ToString(), "Resources", Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Where(FR))
            .SetDescriptionHandler(r => $"{r.GetType().FullName} ({r.GetInstanceID()})")
            .AddAction("select", o => Selection.activeObject = o)
            .AddAction("copy", "Copy Name", r => EditorGUIUtility.systemCopyBuffer = r.name)
            .AddOption(ShowDetailsOptions.Actions | ShowDetailsOptions.Inspector)
            .AddOption(EasyOptions.YieldAllItemsIfSearchQueryEmpty);
    }

    /// <summary>
    /// Search all Unity projects in upper directories
    /// </summary>
    /// <returns></returns>
    struct UnityProject
    {
        private int m_AssetCount;
        private string m_Version;

        public readonly string path;
        public readonly System.IO.FileInfo versionFile;
        public readonly System.IO.DirectoryInfo assetsDir;

        public string name => System.IO.Path.GetFileName(path);
        public string version => m_Version ?? (m_Version = System.IO.File.ReadAllText(versionFile.FullName));
        public int assetCount => m_AssetCount != -1 ? m_AssetCount : (m_AssetCount = assetsDir.GetFiles("*.meta", System.IO.SearchOption.AllDirectories).Length);

        //[SearchItemProvider]
        public static SearchProvider ExampleProjects()
        {
            var icon = EditorGUIUtility.FindTexture("sv_icon_dot8_pix16_gizmo");
            return EasySearchProvider.Create(ExampleProvider.project.ToString(), "Projects", Enumerate("U:/tests"))
                .SetThumbnailHandler(_ => icon)
                .SetDescriptionHandler((p, _) => $"{p.path} ({p.assetCount})")
                .AddAction("select", "Reveal", p => EditorUtility.RevealInFinder(p.path))
                .AddAction("open", p => p.Open())
                .AddOption(EasyOptions.SortByName);
        }

        private void Open()
        {
            string unityExePath = Environment.GetCommandLineArgs()[0];
            System.Diagnostics.Process.Start(unityExePath, string.Join(" ", new [] { "-projectpath", $"\"{path}\""}));
        }

        public UnityProject(System.IO.FileInfo pv) : this()
        {
            var projectDir = pv.Directory.Parent;

            m_AssetCount = -1;
            versionFile = pv;
            path = projectDir.FullName.Replace("\\", "/");
            assetsDir = projectDir.GetDirectories("Assets", System.IO.SearchOption.TopDirectoryOnly).FirstOrDefault();
        }

        static IEnumerable<UnityProject> Enumerate(string currentPath)
        {
            var dir = new System.IO.DirectoryInfo(currentPath);
            if (!dir.Exists)
                return Enumerable.Empty<UnityProject>();
            return FindProjects(dir);
        }

        private static IEnumerable<UnityProject> FindProjects(System.IO.DirectoryInfo dir)
        {
            string[] s_InvalidScanDirectories = new[] { ".", "Library", "obj" };

            var subdirs = dir.GetDirectories().Where(d => s_InvalidScanDirectories.All(si => !d.Name.StartsWith(si, StringComparison.OrdinalIgnoreCase))).ToArray();
            foreach (var d in subdirs)
            {
                if (d.Name == "ProjectSettings")
                {
                    var pv = d.GetFiles().Where(f => string.Equals(f.Name, "ProjectVersion.txt", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    if (pv != null)
                    {
                        yield return new UnityProject(pv);
                        break;
                    }
                }
                else
                {
                    foreach (var pv in FindProjects(d))
                        yield return pv;
                }
            }
        }

        public override string ToString() => path;
    }

    [MenuItem("Window/Search/Easy")] 
    public static void ShowProvider()
    {
        SearchService.ShowContextual(Enum.GetValues(typeof(ExampleProvider)).Cast<ExampleProvider>().Select(e => e.ToString()).ToArray());
    }
}

