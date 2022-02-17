using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Search;
using UnityEditor.Search.Providers;
using UnityEngine;

[Flags]
enum EasyOptions
{
    None = 0,

    YieldAllItemsIfSearchQueryEmpty = 1 << 5,
    DescriptionSameAsLabel = 1 << 6,
    SortByName = 1 << 7,
    DisplayFilterValueInDescription = 1 << 8,
}

static class EasyOptionsExtensions
{
    public static bool HasAny(this EasyOptions flags, in EasyOptions f) => (flags & f) != 0;
    public static bool HasAll(this EasyOptions flags, in EasyOptions all) => (flags & all) == all;
}

static class EasySearchProvider
{
    public static EasySearchProvider<T> Create<T>(string id, Func<SearchContext, IEnumerable<T>> fetchObjects)
    {
        return Create(id, null, fetchObjects);
    }

    public static EasySearchProvider<T> Create<T>(Func<SearchContext, IEnumerable<T>> fetchObjects)
    {
        return Create(typeof(T).Name.ToLowerInvariant(), null, fetchObjects);
    }

    public static EasySearchProvider<T> Create<T>(string id, string label, Func<SearchContext, IEnumerable<T>> fetchObjects)
    {
        return new EasySearchProvider<T>(id, label, fetchObjects);
    }

    public static EasySearchProvider<T> Create<T>(string id, string label, IEnumerable<T> objects)
    {
        return new EasySearchProvider<T>(id, label, objects);
    }
}

readonly struct EasyFilter
{
    public readonly string name;
    public readonly string label;
    public readonly Func<object, object> func;

    public EasyFilter(string name, string label, Func<object, object> func)
    {
        this.name = name;
        this.label = label;
        this.func = func;
    }
}

class EasySearchProvider<T> : SearchProvider
{
    private bool m_FiltersAdded;
    private EasyOptions m_Options;
    private QueryEngine<T> m_QueryEngine;
    private List<EasyFilter> m_Filters;

    private T[] m_CachedObjects;
    private Func<SearchContext, IEnumerable<T>> m_FetchObjects;

    public EasySearchProvider(string id, string displayName, IEnumerable<T> objects)
        : this(id, displayName, null as Func<SearchContext, IEnumerable<T>>)
    {
        m_CachedObjects = objects.ToArray();
        m_FetchObjects = _ => m_CachedObjects;
    }

    public EasySearchProvider(string id, string displayName, Func<SearchContext, IEnumerable<T>> fetchObjects) 
        : base(id, displayName ?? ObjectNames.NicifyVariableName(typeof(T).Name))
    {
        active = false;
        m_FetchObjects = fetchObjects;
        m_Filters = new List<EasyFilter>();

        showDetails = false;
        showDetailsOptions = ShowDetailsOptions.None;
        fetchItems = (context, items, provider) => FetchItems(context);
        fetchPropositions = FetchPropositions;
        fetchDescription = FetchDescription;
        fetchThumbnail = FetchThumbnail;
        startDrag = StartDrag;
        toObject = ToObject;

        m_QueryEngine = new QueryEngine<T>();
        m_QueryEngine.SetSearchDataCallback(SearchWords, s => s.ToLowerInvariant(), StringComparison.Ordinal);
        SearchValue.SetupEngine(m_QueryEngine);

        var dataType = typeof(T);
        if (!dataType.IsPrimitive && dataType != typeof(string))
        {
            AddByReflectionFilters();
            AddFilter("t", "Object Type", obj => obj.GetType().Name);
        }
    }

    public EasySearchProvider<T> AddOption(in EasyOptions options)
    {
        m_Options |= options;
        return this;
    }

    public EasySearchProvider<T> AddOption(ShowDetailsOptions showDetailsOptions)
    {
        this.showDetailsOptions |= showDetailsOptions;
        this.showDetails = this.showDetailsOptions != ShowDetailsOptions.None;
        return this;
    }

    public EasySearchProvider<T> AddAction(string name, Action<T> handler)
    {
        actions.Insert(0, new SearchAction(id, name,
            new GUIContent(ObjectNames.NicifyVariableName(name)),
            (items) => ActionToDataHandler(items, handler)));
        return this;
    }

    public EasySearchProvider<T> AddAction(string name, string label, Action<T> handler)
    {
        actions.Insert(Math.Min(1, actions.Count), new SearchAction(id, name, new GUIContent(label),
            (items) => ActionToDataHandler(items, handler)));
        return this;
    }

    public EasySearchProvider<T> RemoveOption(ShowDetailsOptions showDetailsOptions)
    {
        this.showDetailsOptions &= ~showDetailsOptions;
        this.showDetails = this.showDetailsOptions != ShowDetailsOptions.None;
        return this;
    }

    public EasySearchProvider<T> SetDescriptionHandler(Func<T, SearchItemOptions, string> handler)
    {
        fetchDescription = (item, context) => FetchObjectDescription(item, context, handler);
        return AddOption(ShowDetailsOptions.Description);
    }

    public EasySearchProvider<T> SetDescriptionHandler(Func<T, string> handler)
    {
        return SetDescriptionHandler((T o, SearchItemOptions options) => handler(o));
    }

    public EasySearchProvider<T> SetThumbnailHandler(Func<T, Texture2D> handler)
    {
        this.fetchThumbnail = (item, context) => FetchObjectThumbnail(item, context, handler);
        return this;
    }

    public EasySearchProvider<T> AddFilter<TResult>(string filter, in string label, Func<T, TResult> func)
    {
        if (m_Filters.Any(f => string.Equals(f.name, filter, StringComparison.Ordinal)))
            return this;

        m_Filters.Add(new EasyFilter(filter, label, o => func((T)o)));
        m_QueryEngine.AddFilter(filter, func);
        return this;
    }

    public EasySearchProvider<T> AddByReflectionActions()
    {
        foreach (var m in typeof(T).GetMethods().OrderBy(m => m.Name))
        {
            if (m.GetParameters().Length > 0 || m.IsStatic)
                continue;

            actions.Add(new SearchAction(id, m.Name) { handler = item => HandleMethod(item, m), closeWindowAfterExecution = false });
        }
        return this;
    }

    void StartDrag(SearchItem item, SearchContext context)
    {
        var data = (T)item.data;
        DragAndDrop.PrepareStartDrag();
        if (data is UnityEngine.Object uo)
            DragAndDrop.objectReferences = new [] { uo };
        else
            DragAndDrop.SetGenericData(item.data.GetType().Name, item.data);
        DragAndDrop.StartDrag(GetName(data));
    }

    string FetchObjectDescription(SearchItem item, SearchContext context, Func<T, SearchItemOptions, string> handler)
    {
        if (item.data is T obj)
            return handler(obj, item.options);
        return null;
    }

    Texture2D FetchObjectThumbnail(SearchItem item, SearchContext context, Func<T, Texture2D> handler)
    {
        if (item.data is T obj)
            return handler(obj);
        return null;
    }

    void ActionToDataHandler(SearchItem[] items, Action<T> handler)
    {
        foreach (var item in items)
        {
            if (item.data is T obj)
                handler(obj);
        }
    }

    void AddByReflectionFilters()
    {
        foreach (var prop in typeof(T).GetProperties())
        {
            var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            var filterName = ObjectNames.NicifyVariableName(prop.Name).Replace(" ", "").ToLowerInvariant();
            AddFilter(filterName, $"C# {type.Name}", obj => FetchPropertyValue(obj, prop));
        }

        foreach (var prop in typeof(T).GetFields())
        {
            var type = Nullable.GetUnderlyingType(prop.FieldType) ?? prop.FieldType;
            var filterName = ObjectNames.NicifyVariableName(prop.Name).Replace(" ", "").ToLowerInvariant();
            AddFilter(filterName, $"C# {type.Name}", obj => FetchPropertyValue(obj, prop));
        }
    }

    IEnumerable<SearchProposition> FetchPropositions(SearchContext context, SearchPropositionOptions options)
    {
        foreach (var f in m_Filters)
            yield return new SearchProposition(label: f.name, f.name, f.label);
    }

    IEnumerable<string> SearchWords(T obj)
    {
        yield return GetName(obj).ToLowerInvariant();
        yield return obj.ToString().ToLowerInvariant();
    }

    string GetName(T obj)
    {
        if (TryGetProperty(obj, "name", out var objName) && objName is string ons && !string.IsNullOrEmpty(ons))
            return ons.ToString();
        return obj.ToString();
    }

    bool TryGetProperty(in T obj, string name, out object value)
    {
        var p = obj.GetType().GetProperty(name);
        if (p != null)
        {
            value = p.GetValue(obj);
            return true;
        }

        value = null;
        return false;
    }

    IEnumerable<SearchItem> FetchItems(SearchContext context)
    {
        if (string.IsNullOrEmpty(context.searchQuery))
        {
            if (HasOption(EasyOptions.YieldAllItemsIfSearchQueryEmpty))
                return SortObjects(m_FetchObjects(context)).Select((o, index) => CreateItem(context, GetName(o), index, o));
            return Enumerable.Empty<SearchItem>();
        }

        if (!m_FiltersAdded)
            AddFilters(m_FetchObjects(context).FirstOrDefault());

        var query = m_QueryEngine.Parse(context.searchQuery);
        if (!query.valid)
        {
            context.AddSearchQueryErrors(query.errors.Select(e => new SearchQueryError(e, context, this)));
            return Enumerable.Empty<SearchItem>();
        }

        return SearchItems(context, query);
    }

    string FetchDescription(SearchItem item, SearchContext context)
    {
        if ((item.options & SearchItemOptions.Compacted) != 0 || m_Options.HasAny(EasyOptions.DescriptionSameAsLabel))
            return item.GetLabel(context);

        if (m_Options.HasAny(EasyOptions.DisplayFilterValueInDescription))
        {
            var description = "";
            foreach (var f in m_Filters)
            {
                if (context.searchQuery.LastIndexOf(f.name) == -1)
                    continue;

                if (description.Length != 0)
                    description += ", ";
                description += $"{f.name}={f.func(item.data)}";
            }

            return description;
        }

        return item.data.ToString();
    }

    private Texture2D FetchThumbnail(SearchItem item, SearchContext context)
    {
        if (item.data is UnityEngine.Object uo)
            return AssetPreview.GetMiniThumbnail(uo);
        return EditorGUIUtility.FindTexture(item.data.GetType().Name);
    }

    private UnityEngine.Object ToObject(SearchItem item, Type type)
    {
        return item.data as UnityEngine.Object;
    }

    IEnumerable<T> SortObjects(IEnumerable<T> items)
    {
        if (m_Options.HasAny(EasyOptions.SortByName))
            items = items.OrderBy(e => GetName(e));
        return items;
    }

    IEnumerable<SearchItem> SearchItems(SearchContext context, Query<T> query)
    {
        int index = 0;
        foreach (var o in SortObjects(m_FetchObjects(context)))
        {
            if (o == null || o.Equals(default(T)))
            {
                yield return null;
                continue;
            }

            if (!query.Test(o))
            {
                yield return null;
                continue;
            }

            var score = index++;
            var name = GetName(o);
            if (!m_Options.HasAny(EasyOptions.SortByName))
                score = ComputeScore(name);
            yield return CreateItem(context, name, score, o);
        }
    }

    void AddFilters(T o)
    {
        if (o == null)
            return;

        if (o is UnityEngine.Object uo)
        {
            using (var so = new SerializedObject(uo))
            {
                var p = so.GetIterator();
                var next = p.NextVisible(true);
                while (next)
                {
                    var propertyPath = p.propertyPath;
                    var filterName = ObjectNames.NicifyVariableName(propertyPath).Replace(" ", "").ToLowerInvariant();

                    AddFilter(filterName, $"{p.displayName} ({p.propertyType})", obj => FetchPropertyValue(obj, propertyPath));
                    next = p.NextVisible(p.hasChildren);
                }
            }
        }

        m_FiltersAdded = true;
    }

    private void HandleMethod(SearchItem item, MethodInfo mi)
    {
        var result = mi.Invoke(item.data, null);
        var unityObject = item.data as UnityEngine.Object;
        if (result != null)
            Debug.Log(result, unityObject);
        else
            Debug.Log($"Executed {mi.DeclaringType.FullName}.{mi.Name}", unityObject);
    }

    SearchValue FetchPropertyValue(in T obj, in FieldInfo prop) => new SearchValue(prop.GetValue(obj));
    SearchValue FetchPropertyValue(in T obj, in PropertyInfo prop) => new SearchValue(prop.GetValue(obj));

    SearchValue FetchPropertyValue(in T obj, in string propertyPath)
    {
        if (obj is UnityEngine.Object uo)
        {
            using (var so = new SerializedObject(uo))
            {
                var p = so.FindProperty(propertyPath);
                if (p == null)
                    return SearchValue.invalid;
                return SearchValue.ConvertPropertyValue(p);
            }
        }

        return SearchValue.invalid;
    }

    SearchItem CreateItem(in SearchContext context, in string name, in int score, in T o)
    {
        if (o == null)
            return null;
        return CreateItem(context, Guid.NewGuid().ToString("N"), score, name, null, null, o);
    }

    int ComputeScore(in string name)
    {
        if (name.Length > 2)
        {
            var sp = Math.Max(0, name.LastIndexOf('/'));
            if (sp + 2 < name.Length)
                return name[sp] * 5 + name[sp + 1] * 2 + name[sp + 2];
        }
        return 99;
    }

    bool HasOption(in EasyOptions option)
    {
        return m_Options.HasAny(option);
    }
}
