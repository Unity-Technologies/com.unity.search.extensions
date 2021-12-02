#if UNITY_2021_2_OR_NEWER
using UnityEngine;
using UnityEditor.Overlays;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace UnityEditor.Search.Collections
{
    [Icon("Icons/QuickSearch/ListView.png")]
    #if UNITY_2022_1_OR_NEWER
    [Overlay(typeof(SceneView), "Collections", defaultLayout = Layout.Panel)]
    #else
    [Overlay(typeof(SceneView), "Collections", defaultLayout: false)]
    #endif
    class SearchCollectionOverlay : ExtendedOverlay, ISearchCollectionHostView, IHasCustomMenu
    {
        static class InnerStyles
        {
            public static GUIContent createContent = EditorGUIUtility.IconContent("CreateAddNew");
            public static GUIStyle toolbarCreateAddNewDropDown = new GUIStyle("ToolbarCreateAddNewDropDown")
            {
                fixedWidth = 32f,
                fixedHeight = 0,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(4, 4, 4, 4)
            };
        }

        [SerializeField] SearchCollectionView m_CollectionView;
        
        public string name => GetName();

        private string GetName()
        {
            if (containerWindow is SceneView sv)
            {
                Scene customScene = (Scene)sv.GetType().GetProperty("customScene", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(sv);
                if (customScene.IsValid())
                    return customScene.name;

                return SceneManager.GetActiveScene().name;
            }
            return containerWindow?.name ?? "overlay";
        }

        public bool overlay => true;
        public bool docked => false;

        public override void OnCreated()
        {
            m_CollectionView = new SearchCollectionView();
            m_CollectionView.Initialize(this);
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        }

        public override void OnWillBeDestroyed()
        {
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            m_CollectionView.SaveCollections();
            base.OnWillBeDestroyed();
        }

        private void OnActiveSceneChanged(Scene arg0, Scene arg1)
        {
            m_CollectionView = new SearchCollectionView();
            m_CollectionView.Initialize(this);
            Repaint();
        }

        protected override void Render(Event evt)
        {
            DrawToolbar(evt);
            m_CollectionView.OnGUI(evt);
        }

        private void DrawToolbar(Event evt)
        {
            var toolbarRect = EditorGUILayout.GetControlRect(false, 21f, GUIStyle.none, GUILayout.ExpandWidth(true));
            var buttonStackRect = HandleButtons(evt, toolbarRect);

            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.None && evt.character == '\r')
                return;

            toolbarRect.xMin = buttonStackRect.xMax + 2f;
            var searchTextRect = toolbarRect;
            searchTextRect = EditorStyles.toolbarSearchField.margin.Remove(searchTextRect);
            searchTextRect.xMax += 1f;
            searchTextRect.y += Mathf.Round((toolbarRect.height - searchTextRect.height) / 2f - 2f);

            m_CollectionView.searchText = EditorGUI.TextField(searchTextRect, m_CollectionView.searchText, EditorStyles.toolbarSearchField);
            DrawButtons(buttonStackRect, evt);
        }

        private Rect HandleButtons(Event evt, Rect toolbarRect)
        {
            Rect rect = toolbarRect;
            rect = InnerStyles.toolbarCreateAddNewDropDown.margin.Remove(rect);
            rect.xMax = rect.xMin + InnerStyles.toolbarCreateAddNewDropDown.fixedWidth;
            rect.y += (toolbarRect.height - rect.height) / 2f - 5f;
            rect.height += 2f;

            bool mouseOver = rect.Contains(evt.mousePosition);
            if (evt.type == EventType.MouseDown && mouseOver)
            {
                GUIUtility.hotControl = 0;

                var menu = new GenericMenu();
                m_CollectionView.AddCollectionMenus(menu);
                menu.ShowAsContext();
                evt.Use();
            }

            return rect;
        }

        void DrawButtons(in Rect buttonStackRect, in Event evt)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            bool mouseOver = buttonStackRect.Contains(evt.mousePosition);
            InnerStyles.toolbarCreateAddNewDropDown.Draw(buttonStackRect, InnerStyles.createContent, mouseOver, false, false, false);
        }

        public void OpenContextualMenu()
        {
            m_CollectionView.OpenContextualMenu();
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddSeparator("");
            m_CollectionView.OpenContextualMenu(menu);            
            menu.AddSeparator("");
        }
    }
}
#endif
