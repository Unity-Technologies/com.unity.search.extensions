#if UNITY_2021_2_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace UnityEditor.Search.Collections
{ 
    class SearchCollectionTreeViewItem : SearchTreeViewItem
    {
#if UNITY_2021_2_OR_NEWER
        public static readonly GUIContent collectionIcon = EditorGUIUtility.IconContent("ListView");
#else
        public static readonly GUIContent collectionIcon = new GUIContent(Icons.quickSearchWindow);
#endif

        readonly SearchCollection m_Collection;
        SearchAction[] m_Actions;
        public SearchCollection collection => m_Collection;
        Action m_AutomaticUpdate;
        bool m_NeedsRefresh;
        Matrix4x4 m_LastCameraPos;

        public SearchCollectionTreeViewItem(SearchCollectionTreeView treeView, SearchCollection collection)
            : base(treeView)
        {
            m_Collection = collection ?? throw new ArgumentNullException(nameof(collection));

            displayName = m_Collection.name;
            children = new List<TreeViewItem>();
            icon = m_Collection.icon ?? (collectionIcon.image as Texture2D);                

            FetchItems();
        }

        public override string GetLabel()
        {
            #if USE_SEARCH_EXTENSION_API
            return $"{(m_AutomaticUpdate != null ? "!" : "")}{m_Collection.name} ({SearchUtils.FormatCount((ulong)children.Count)})";
            #else
            return $"{(m_AutomaticUpdate != null ? "!" : "")}{m_Collection.name}";
            #endif
        }

        private void AddObjects(IEnumerable<UnityEngine.Object> objs)
        {
            foreach (var obj in objs)
            {
                if (!obj)
                    continue;
                AddChild(new SearchObjectTreeViewItem(m_TreeView, obj));
            }
        }

        public void FetchItems()
        {
            AddObjects(m_Collection.objects);

            if (string.IsNullOrEmpty(m_Collection.searchText))
                return;

            var providers = m_Collection?.providerIds.Length == 0 ? SearchService.GetActiveProviders().Select(p => p.id) : m_Collection.providerIds;
            var context = SearchService.CreateContext(providers, m_Collection.searchText);
            foreach (var item in m_Collection.items)
                AddChild(new SearchTreeViewItem(m_TreeView, context, item));
            SearchService.Request(context, (_, items) =>
            {
                foreach (var item in items)
                {
                    if (m_Collection.items.Add(item))
                        AddChild(new SearchTreeViewItem(m_TreeView, context, item));
                }
            },
            _ =>
            {
                m_TreeView.UpdateCollections();
                context?.Dispose();
            });
        }

        public override void Select()
        {
            // Do nothing
        }

        public override void Open()
        {
            m_TreeView.SetExpanded(id, !m_TreeView.IsExpanded(id));
        }

        public override bool CanStartDrag()
        {
            return false;
        }

        public override void OpenContextualMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Refresh"), false, () => Refresh());
            menu.AddItem(new GUIContent("Automatic Update"), m_AutomaticUpdate != null, () => ToggleAutomaticUpdate());

            var selection = Selection.objects;
            if (selection.Length > 0)
            {
                menu.AddSeparator("");
                if (selection.Length == 1)
                    menu.AddItem(new GUIContent($"Add {selection[0].name}"), false, AddSelection);
                else
                    menu.AddItem(new GUIContent($"Add selection ({selection.Length} objects)"), false, AddSelection);
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Set Color"), false, SelectColor);
            #if USE_SEARCH_EXTENSION_API
            menu.AddItem(new GUIContent("Set Icon"), false, SetIcon);
            #endif
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Rename"), false, () => m_TreeView.BeginRename(this));
            menu.AddItem(new GUIContent("Remove"), false, () => m_TreeView.Remove(this, m_Collection));

            menu.ShowAsContext();
        }

        private void AddSelection()
        {
            AddObjectsToTree(Selection.objects);
        }

        internal void AddObjectsToTree(UnityEngine.Object[] objects)
        {
            m_Collection.AddObjects(objects);
            AddObjects(objects);
            m_TreeView.UpdateCollections();
            m_TreeView.SaveCollections();
        }

        #if USE_SEARCH_EXTENSION_API
        private void SetIcon()
        {
            SearchUtils.ShowIconPicker((newIcon, canceled) =>
            {
                if (canceled)
                    return;
                icon = m_Collection.icon = newIcon;
                m_TreeView.SaveCollections();
                m_TreeView.Repaint();
            });
        }
        #endif

        internal void DrawActions(in Rect rowRect, in GUIStyle style)
        {
            var buttonRect = rowRect;
            buttonRect.y += 2f;
            buttonRect.xMin = buttonRect.xMax - 22f;

            var buttonCount = 0;
            var items = GetSceneItems();
            foreach (var a in GetActions())
            {
                if (a.execute == null || !a.content.image)
                    continue;

                if (items.Count == 0 || (a.enabled != null && !a.enabled(items)))
                    continue;
                if (GUI.Button(buttonRect, a.content, style))
                    ExecuteAction(a, items);
                buttonRect.x -= 20f;
                buttonCount++;
            }
        }

        IEnumerable<SearchAction> GetActions()
        {
            if (m_Actions == null)
            {
                var sceneProvider = SearchService.GetProvider("scene");
                m_Actions = sceneProvider.actions.Reverse<SearchAction>().ToArray();
            }
            return m_Actions;
        }

        private IReadOnlyCollection<SearchItem> GetSceneItems()
        {
            var items = new HashSet<SearchItem>();
            var sceneProvider = SearchService.GetProvider("scene");
            foreach (var c in children)
            {
                if (c is SearchObjectTreeViewItem otvi && otvi.GetObject() is GameObject go)
                {
                    #if USE_SEARCH_EXTENSION_API
                    items.Add(SearchUtils.CreateSceneResult(null, sceneProvider, go));
                    #else
                    items.Add(Providers.SceneProvider.AddResult(null, sceneProvider, go));
                    #endif
                }
                else if (c is SearchTreeViewItem tvi && string.Equals(tvi.item.provider.type, "scene", StringComparison.Ordinal))
                    items.Add(tvi.item);
            }
            return items;
        }

        private void ExecuteAction(in SearchAction a, IReadOnlyCollection<SearchItem> items)
        {
            a.execute(items.ToArray());
        }

        private void SelectColor()
        {
            var c = collection.color;
            var colorPickerType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ColorPicker");
            var showMethod = colorPickerType.GetMethod("Show", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public, null, 
                new [] { typeof(Action<Color>), typeof(Color), typeof(bool), typeof(bool) }, null);
            Action<Color> setColorDelegate = SetColor;
            showMethod.Invoke(null, new object[] { setColorDelegate, new Color(c.r, c.g, c.b, 1.0f), false, false });
        }

        private void SetColor(Color color)
        {
            m_Collection.color = color;
            EditorApplication.delayCall -= m_TreeView.SaveCollections;
            EditorApplication.delayCall += m_TreeView.SaveCollections;
        }

        public void Refresh()
        {
            children.Clear();
            m_Collection.items.Clear();
            FetchItems();
        }

        private void ToggleAutomaticUpdate()
        {
            if (m_AutomaticUpdate != null)
                ClearAutoRefresh();
            else
                SetAutoRefresh();
        }

        public void AutoRefresh()
        {
            var camPos = SceneView.lastActiveSceneView?.camera.transform.localToWorldMatrix ?? Matrix4x4.identity;
            if (camPos != m_LastCameraPos)
            {
                NeedsRefresh();
                m_LastCameraPos = camPos;
            }

            if (m_NeedsRefresh)
                Refresh();
            if (m_AutomaticUpdate != null)
                SetAutoRefresh();
        }

        private void ClearAutoRefresh()
        {
            if (m_AutomaticUpdate!=null)
            {
                m_AutomaticUpdate();
                m_AutomaticUpdate = null;
            }
            m_NeedsRefresh = false;
            ObjectChangeEvents.changesPublished -= OnObjectChanged;
        }

        public void SetAutoRefresh()
        {
            ClearAutoRefresh();
            ObjectChangeEvents.changesPublished += OnObjectChanged;
            #if USE_SEARCH_EXTENSION_API
            m_AutomaticUpdate = Dispatcher.CallDelayed(AutoRefresh, 0.9d);
            #else
            m_AutomaticUpdate = Utils.CallDelayed(AutoRefresh, 0.9d);
            #endif
        }

        private void NeedsRefresh() => m_NeedsRefresh = true;
        private void OnObjectChanged(ref ObjectChangeEventStream stream)
        {
            for (int i = 0; i < stream.length; ++i)
            {
                var eventType = stream.GetEventType(i);
                switch (eventType)
                {
                    case ObjectChangeKind.ChangeScene:
                    case ObjectChangeKind.CreateGameObjectHierarchy:
                    case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                    case ObjectChangeKind.ChangeGameObjectStructure:
                    case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                    case ObjectChangeKind.DestroyGameObjectHierarchy:
                        NeedsRefresh();
                        break;
                }
            }
        }
    }
}
#endif
