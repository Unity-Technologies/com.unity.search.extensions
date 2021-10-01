#if UNITY_2021_2_OR_NEWER
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
using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace UnityEditor.Search.Collections
{
    [Overlay(typeof(SceneView), "Collections", defaultLayout: false)]
    class SearchCollectionOverlay : Overlay, ISearchCollectionView, IHasCustomMenu
    {
        static class InnerStyles
        {
            public static readonly GUIContent collectionIcon = EditorGUIUtility.IconContent("ListView");
        }

        enum ResizingWindow
        {
            None,
            Left,
            Right,
            Bottom,
            Gripper
        }
        [SerializeField] string m_SearchText;
        [SerializeField] TreeViewState m_TreeViewState;
        [SerializeField] List<SearchCollection> m_Collections;

        SearchCollectionTreeView m_TreeView;
        IMGUIContainer m_CollectionContainer;
        ResizingWindow m_Resizing = ResizingWindow.None;

        static int RESIZER_CONTROL_ID = "SearchCollectionOverlayResizer".GetHashCode();
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

        public SearchCollectionOverlay()
        {
            if (m_TreeViewState == null)
                m_TreeViewState = new TreeViewState();

            if (m_Collections == null)
                m_Collections = LoadCollections();

            m_TreeView = new SearchCollectionTreeView(m_TreeViewState, this);

            layout = Layout.Panel;
        }

        public override VisualElement CreatePanelContent()
        {
            rootVisualElement.pickingMode = PickingMode.Position;
            m_CollectionContainer = new IMGUIContainer(OnGUI);
            m_CollectionContainer.style.width = EditorPrefs.GetFloat("SCO_Width", 250f);
            m_CollectionContainer.style.height = EditorPrefs.GetFloat("SCO_Height", 350f);
            return m_CollectionContainer;
        }

        private void OnGUI()
        {
            if (!displayed)
                return;

            var evt = Event.current;
            HandleShortcuts(evt);
            HandleOverlayResize(evt);
            DrawTreeView();
        }

        private void HandleOverlayResize(Event evt)
        {
            if (evt.type == EventType.MouseUp)
            {
                GUIUtility.hotControl = 0;
                m_Resizing = ResizingWindow.None;
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && GUIUtility.hotControl == RESIZER_CONTROL_ID)
            {
                switch (m_Resizing)
                {
                    case ResizingWindow.Left:
                        var mousePositionUnclipped = GUIClip.UnclipToWindow(evt.mousePosition);
                        var diff = rootVisualElement.style.left.value.value - mousePositionUnclipped.x;
                        rootVisualElement.style.left = mousePositionUnclipped.x;
                        m_CollectionContainer.style.width = m_CollectionContainer.style.width.value.value + diff;
                        break;
                    case ResizingWindow.Right:
                        m_CollectionContainer.style.width = evt.mousePosition.x;
                        break;
                    case ResizingWindow.Bottom:
                        m_CollectionContainer.style.height = evt.mousePosition.y;
                        break;
                    case ResizingWindow.Gripper:
                        m_CollectionContainer.style.width = evt.mousePosition.x;
                        m_CollectionContainer.style.height = evt.mousePosition.y;
                        break;
                    default:
                        return;
                }

                EditorPrefs.SetFloat("SCO_Width", m_CollectionContainer.style.width.value.value);
                EditorPrefs.SetFloat("SCO_Height", m_CollectionContainer.style.height.value.value);
                evt.Use();
            }
            else
            {
                const float resizeGripperSize = 3;
                var width = m_CollectionContainer.style.width.value.value;
                var height = m_CollectionContainer.style.height.value.value;

                var leftResizeRect = new Rect(0, 0, resizeGripperSize, height);
                var rightResizeRect = new Rect(width - resizeGripperSize, 0, resizeGripperSize, height - resizeGripperSize * 2);
                var bottomResizeRect = new Rect(0, height - resizeGripperSize, width - resizeGripperSize * 2, resizeGripperSize);
                var bottomRightResizeRect = new Rect(width - resizeGripperSize * 2, height - resizeGripperSize * 2, resizeGripperSize * 2, resizeGripperSize * 2);

                EditorGUIUtility.AddCursorRect(leftResizeRect, MouseCursor.ResizeHorizontal);
                EditorGUIUtility.AddCursorRect(rightResizeRect, MouseCursor.ResizeHorizontal);
                EditorGUIUtility.AddCursorRect(bottomResizeRect, MouseCursor.ResizeVertical);
                EditorGUIUtility.AddCursorRect(bottomRightResizeRect, MouseCursor.ResizeUpLeft);
                
                if (evt.type == EventType.MouseDown)
                {
                    if (bottomRightResizeRect.Contains(evt.mousePosition))
                        m_Resizing = ResizingWindow.Gripper;
                    else if (leftResizeRect.Contains(evt.mousePosition))
                        m_Resizing = ResizingWindow.Left;
                    else if (rightResizeRect.Contains(evt.mousePosition))
                        m_Resizing = ResizingWindow.Right;
                    else if (bottomResizeRect.Contains(evt.mousePosition))
                        m_Resizing = ResizingWindow.Bottom;
                    else
                        return;

                    if (m_Resizing != ResizingWindow.None && evt.isMouse)
                    {
                        GUIUtility.hotControl = RESIZER_CONTROL_ID;
                        evt.Use();
                    }
                }
            }
        }

        private ISet<string> EnumerateFieldNames()
        {
            var names = new HashSet<string>();
            foreach (var c in m_Collections)
                foreach (var e in c.items)
                    names.UnionWith(e.GetFieldNames());
            return names;
        }

        private List<SearchCollection> LoadCollections()
        {
            var collectionPaths = EditorPrefs.GetString("SearchCollections", "")
                .Split(new[] { ";;;" }, StringSplitOptions.RemoveEmptyEntries);
            var collection = collectionPaths
                .Select(p => AssetDatabase.LoadAssetAtPath<SearchQueryAsset>(p))
                .Where(p => p)
                .Select(sq => new SearchCollection(sq));
            return new List<SearchCollection>(collection);
        }

        public void SaveCollections()
        {
            var collectionPaths = string.Join(";;;", m_Collections.Select(c => AssetDatabase.GetAssetPath(c.query)));
            EditorPrefs.SetString("SearchCollections", collectionPaths);
        }

        void HandleShortcuts(Event evt)
        {
            if (evt.type == EventType.KeyUp && evt.keyCode == KeyCode.F5)
            {
                m_TreeView.Reload();
                evt.Use();
            }
        }

        void DrawTreeView()
        {
            var treeViewRect = EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true));
            m_TreeView.OnGUI(treeViewRect);
        }

        public void AddCollectionMenus(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Load collection..."), false, LoadCollection);
        }

        private void LoadCollection()
        {
            var context = SearchService.CreateContext(SearchService.GetObjectProviders(), $"t:{nameof(SearchQueryAsset)}");
            SearchService.ShowPicker(context, SelectCollection,
                trackingHandler: _ => { },
                title: "search collection",
                defaultWidth: 300, defaultHeight: 500, itemSize: 0);
        }

        private void SelectCollection(SearchItem selectedItem, bool canceled)
        {
            if (canceled)
                return;

            var searchQuery = selectedItem.ToObject<SearchQueryAsset>();
            if (!searchQuery)
                return;

            m_TreeView.Add(new SearchCollection(searchQuery));
            SaveCollections();
        }

        void UpdateView()
        {
            m_TreeView.searchString = m_SearchText;
        }

        public void OpenContextualMenu()
        {
            var menu = new GenericMenu();

            AddCollectionMenus(menu);

            menu.ShowAsContext();
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddSeparator("");
            AddCollectionMenus(menu);
            menu.AddItem(new GUIContent("Refresh"), false, () => m_TreeView.Reload());
            menu.AddSeparator("");
        }
    }
}
#endif
