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
        public SearchCollection collection => m_Collection;

        public SearchCollectionTreeViewItem(SearchCollectionTreeView treeView, SearchCollection collection)
            : base(treeView)
        {
            m_Collection = collection ?? throw new ArgumentNullException(nameof(collection));

            icon = m_Collection.icon != null ? m_Collection.icon : (collectionIcon.image as Texture2D);
            displayName = m_Collection.name;
            children = new List<TreeViewItem>();

            FetchItems();
        }

        public void FetchItems()
        {
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
                UpdateLabel();
                m_TreeView.UpdateCollections();
                context?.Dispose();
            });
        }

        private void UpdateLabel()
        {
            displayName = $"{m_Collection.name} ({children.Count})";
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
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Set Color"), false, SelectColor);
            if (m_Collection.searchQuery != null)
                menu.AddItem(new GUIContent("Edit"), false, () => SearchQuery.Open(m_Collection.searchQuery, SearchFlags.None));
            
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Remove"), false, () => m_TreeView.Remove(this, m_Collection));

            menu.ShowAsContext();
        }
        
        private void SelectColor()
        {
            var c = collection.color;
            var colorPickerType = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.ColorPicker");
            var showMethod = colorPickerType.GetMethod("Show", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public, null, 
                new [] { typeof(Action<Color>), typeof(Color), typeof(bool), typeof(bool) }, null);
            Action<Color> setColorDelegate = SetColor;
            showMethod.Invoke(null, new object[] { setColorDelegate, new Color(c.r, c.g, c.b, 1.0f), false, false });
        }

        private void SetColor(Color color)
        {
            m_Collection.color = color;
        }

        public void Refresh()
        {
            children.Clear();
            m_Collection.items.Clear();
            FetchItems();
        }

        public override bool DrawRow(Rect rowRect)
        {
            return true;
        }
    }
}
