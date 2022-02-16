#if UNITY_2021_2_OR_NEWER
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace UnityEditor.Search.Collections
{
    interface ISearchCollectionHostView
    {
        string name { get; }
        bool overlay { get; }
        bool docked { get; }

        void Repaint();
        void Close();
    }

    [Serializable]
    class SearchCollectionView : ISearchCollectionView
    {
        [SerializeField] string m_SearchText;
        [SerializeField] TreeViewState m_TreeViewState;
        [SerializeField] List<SearchCollection> m_Collections;

        SearchCollectionTreeView m_TreeView;
        ISearchCollectionHostView m_HostView;

        public string searchText 
        { 
            get => m_SearchText;
            set  
            {
                if (!string.Equals(value, m_SearchText, StringComparison.Ordinal))
                {
                    m_SearchText = value;
                    UpdateView();
                }
            }
        }

        public bool overlay => m_HostView.overlay;
        public ICollection<SearchCollection> collections => m_Collections;
        
        public void Initialize(ISearchCollectionHostView hostView)
        {
            m_HostView = hostView;

            if (m_TreeViewState == null)
                m_TreeViewState = new TreeViewState();

            if (m_Collections == null)
                m_Collections = SearchCollection.LoadCollections(hostView.name);

            m_TreeView = new SearchCollectionTreeView(m_TreeViewState, this);
        }

        public void SaveCollections()
        {
            SearchCollection.SaveCollections(m_Collections, m_HostView.name);
        }

        public void OnGUI(Event evt)
        {
            HandleShortcuts(evt);
            DrawTreeView();
        }

        void HandleShortcuts(Event evt)
        {
            if (evt.type == EventType.KeyUp && evt.keyCode == KeyCode.F5)
            {
                m_TreeView.Reload();
                evt.Use();
            }
            else if (!m_HostView.docked && evt.type == EventType.KeyUp && evt.keyCode == KeyCode.Escape)
            {
                evt.Use();
                m_HostView.Close();
            }

            if (evt.type == EventType.Used)
                m_HostView.Repaint();
        }

        void DrawTreeView()
        {
            var treeViewRect = EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true));
            m_TreeView.OnGUI(treeViewRect);
        }

        public void AddCollectionMenus(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("New collection"), false, () => m_TreeView.Add(new SearchCollection("Collection")));
            menu.AddItem(new GUIContent("Load collection..."), false, () => SearchCollection.SelectCollection(sq => m_TreeView.Add(sq)));
        }

        void UpdateView()
        {
            m_TreeView.searchString = m_SearchText;
        }

        public void OpenContextualMenu()
        {
            var menu = new GenericMenu();
            OpenContextualMenu(menu);
            menu.ShowAsContext();
        }

        public void OpenContextualMenu(GenericMenu menu)
        {
            AddCollectionMenus(menu);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Refresh"), false, () => m_TreeView.Reload());
            menu.AddItem(new GUIContent("Save"), false, () => SaveCollections());
        }
    }
}
#endif
