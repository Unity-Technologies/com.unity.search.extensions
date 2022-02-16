#if UNITY_2021_2_OR_NEWER
using UnityEngine;

namespace UnityEditor.Search.Collections
{
    class SearchCollectionWindow : EditorWindow, ISearchCollectionHostView
    {
        [MenuItem("Window/Search/Collections")]
        public static void ShowWindow()
        {
            SearchCollectionWindow wnd = GetWindow<SearchCollectionWindow>();
            wnd.titleContent = new GUIContent("Collections");
        }

        static class Content
        {
#if UNITY_2021_2_OR_NEWER
            public static readonly GUIContent icon = EditorGUIUtility.IconContent("ListView");
#else
            public static readonly GUIContent icon = new GUIContent(Icons.quickSearchWindow);
#endif
        }

        [SerializeField] SearchCollectionView m_CollectionView;

        string ISearchCollectionHostView.name => Application.productName;
        public bool overlay => false;
        
        internal void OnEnable()
        {
            titleContent.image = Content.icon.image;
            m_CollectionView = m_CollectionView ?? new SearchCollectionView();
            m_CollectionView.Initialize(this);
        }

        internal void OnDisable()
        {
            m_CollectionView.SaveCollections();
        }

        internal void OnGUI()
        {
            var evt = Event.current;
            m_CollectionView.OnGUI(evt);
        }

        public void OpenContextualMenu()
        {
            m_CollectionView.OpenContextualMenu();
        }
    }
}
#endif
