using System.Collections;
using System.Collections.Generic;
using UnityEditor.Search;
using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;

public static class CustomIndexerUtilities
{
    public static System.Type GetSearchDataBaseType()
    {
        return typeof(ObjectIndexer).Assembly.GetType("UnityEditor.Search.SearchDatabase");
    }

    public static System.Type GetAssetProviderType()
    {
        return typeof(ObjectIndexer).Assembly.GetType("UnityEditor.Search.Providers.AssetProvider");
    }

    public static List<string> GetResultPaths(IEnumerable<SearchResult> results)
    {
        var getAssetPathFunction = GetAssetProviderType().GetMethod("GetAssetPath", new[] { typeof(string) });
        var paths = new List<string>();
        foreach (var item in results)
            paths.Add((string)getAssetPathFunction.Invoke(null, new object[] { item.id }));
        return paths;
    }

    public static object CreateSearchDatabaseSettings(string root, string indexType, bool types, bool properties, bool dependencies, bool extended, string[] includes = null)
    {
        var searchDataBaseType = GetSearchDataBaseType();
        var settingsType = searchDataBaseType.GetNestedType("Settings");
        var optionsType = searchDataBaseType.GetNestedType("Options");

        var options = Activator.CreateInstance(optionsType);
        optionsType.GetField("types").SetValue(options, types);
        optionsType.GetField("properties").SetValue(options, properties);
        optionsType.GetField("dependencies").SetValue(options, dependencies);
        optionsType.GetField("extended").SetValue(options, extended);

        var settings = Activator.CreateInstance(settingsType);
        settingsType.GetField("name").SetValue(settings, System.Guid.NewGuid().ToString("N"));
        settingsType.GetField("guid").SetValue(settings, System.Guid.NewGuid().ToString("N"));
        settingsType.GetField("type").SetValue(settings, indexType);
        settingsType.GetField("roots").SetValue(settings, new[] { root });
        settingsType.GetField("includes").SetValue(settings, includes ?? new string[0]);
        settingsType.GetField("options").SetValue(settings, options);

        return settings;
    }

    public static ObjectIndexer CreateObjectIndexer(object settings)
    {
        var searchDataBaseType = GetSearchDataBaseType();
        var settingsType = searchDataBaseType.GetNestedType("Settings");
        var createIndexerMethod = searchDataBaseType.GetMethod("CreateIndexer", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { settingsType, typeof(string) }, null);
        return (ObjectIndexer)createIndexerMethod.Invoke(null, new object[] { settings, null });
    }

    public static ObjectIndexer CreateIndexer(string root, string indexType, bool types, bool properties, bool dependencies, bool extended, string[] includes = null)
    {
        var settings = CreateSearchDatabaseSettings(root, indexType, types, properties, dependencies, extended, includes);
        return CreateObjectIndexer(settings);
    }

    public static List<string> GetDependencies(ObjectIndexer indexer)
    {
        var getDependenciesMethod = typeof(ObjectIndexer).GetMethod("GetDependencies", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (List<string>)getDependenciesMethod.Invoke(indexer, new object[0]);
    }

    public static IEnumerator RunIndexingAsync(ObjectIndexer indexer, bool clear = false)
    {
        indexer.Start(clear);
        var paths = GetDependencies(indexer);
        foreach (var path in paths)
            indexer.IndexDocument(path, false);
        indexer.Finish();
        while (!indexer.IsReady())
            yield return null;
    }

    public static void RunIndexing(ObjectIndexer indexer, bool clear, Action isDone)
    {
        var enumerator = RunIndexingAsync(indexer, clear);
        Tick(enumerator, isDone);
    }

    public static void Tick(IEnumerator enumerator, Action isDone)
    {
        if (enumerator.MoveNext())
        {
            EditorApplication.delayCall += () => Tick(enumerator, isDone);
        }
        else
        {
            isDone();
        }
    }

    public static List<string> Search(ObjectIndexer indexer, string query)
    {
        var results = indexer.Search(query, null, null);
        return GetResultPaths(results);
    }

    [MenuItem("Tools/Simulate Indexation")]
    static void SimulateIndexation()
    {
        var asset = Selection.activeObject;
        if (asset == null)
            return;
        var path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path))
            return;

        var indexer = CreateIndexer("Assets", "asset", true, true, true, false, new[] { path });
        RunIndexing(indexer, false, () =>
        {
            var results = Search(indexer, "t:material");
            Debug.Log(results);
        });
    }
}