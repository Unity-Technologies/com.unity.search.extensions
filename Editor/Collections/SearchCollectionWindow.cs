// TODO:
// 1- Add a new flags to saved search query to mark them as collection.
//   a. Only load search query asset marked as collections.
// 2- Add support to create search query asset with a custom list of search items.

// PICKER ISSUES:
// - Hide toolbar/search field/button
// - Allow to toggle panels
// - Do not always center the picker view
// - Allow to completely override the picker title (do not keep Select ...)
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace UnityEditor.Search.Collections
{
    class SearchCollectionWindow : EditorWindow, ISearchCollectionView
    {
        static class InnerStyles
        {
            #if UNITY_2021_2_OR_NEWER
            public static readonly GUIContent collectionIcon = EditorGUIUtility.IconContent("ListView");
            #else
            public static readonly GUIContent collectionIcon = new GUIContent(Icons.quickSearchWindow);
            #endif
        }

        SearchCollectionTreeView m_TreeView;

        [SerializeField] string m_SearchText;
        [SerializeField] TreeViewState m_TreeViewState;
        [SerializeField] List<SearchCollection> m_Collections;

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

        public ICollection<SearchCollection> collections => m_Collections;
        public ISet<string> fieldNames => EnumerateFieldNames();

        private ISet<string> EnumerateFieldNames()
        {
            var names = new HashSet<string>();
            foreach (var c in m_Collections)
                foreach (var e in c.items)
                    names.UnionWith(e.GetFieldNames());
            return names;
        }

        internal void OnEnable()
        {
            titleContent.image = InnerStyles.collectionIcon.image;
            
            if (m_TreeViewState == null)
                m_TreeViewState = new TreeViewState();

            if (m_Collections == null)
                m_Collections = LoadCollections();

            m_TreeView = new SearchCollectionTreeView(m_TreeViewState, this);
        }

        internal void OnDisable()
        {
            SaveCollections();
        }

        [Serializable]
        class SearchCollections
        {
            public const string key = "SearchCollections_V2";

            public List<SearchCollection> collections;

            public SearchCollections()
            {
                collections = new List<SearchCollection>();
            }

            public SearchCollections(IEnumerable<SearchCollection> collections)
            {
                this.collections = collections.ToList();
            }
        }

        private List<SearchCollection> LoadCollections()
        {
            return SearchCollection.LoadCollections();
        }

        public void SaveCollections()
        {
            SearchCollection.SaveCollections(m_Collections);
        }

        void OnGUI()
        {
            var evt = Event.current;
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
            else if (!docked && evt.type == EventType.KeyUp && evt.keyCode == KeyCode.Escape)
            {
                evt.Use();
                Close();
            }

            if (evt.type == EventType.Used)
                Repaint();
        }

        void DrawTreeView()
        {
            var treeViewRect = EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true));
            m_TreeView.OnGUI(treeViewRect);
        }

        public void AddCollectionMenus(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Load collection..."), false, () => SearchCollection.SelectCollection(sq => m_TreeView.Add(sq)));
        }

        void UpdateView()
        {
            m_TreeView.searchString = m_SearchText;
        }

        [MenuItem("Window/Search/Collections")]
        public static void ShowWindow()
        {
            SearchCollectionWindow wnd = GetWindow<SearchCollectionWindow>();
            wnd.titleContent = new GUIContent("Collections");
        }

        public void OpenContextualMenu()
        {
            var menu = new GenericMenu();

            AddCollectionMenus(menu);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Refresh"), false, () => m_TreeView.Reload());
            menu.ShowAsContext();
        }
    }
}
