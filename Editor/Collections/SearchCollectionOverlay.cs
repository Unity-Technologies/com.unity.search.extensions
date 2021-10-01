#if UNITY_2021_2_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEditor.Overlays;
using UnityEngine.UIElements;
using System.Linq;

namespace UnityEditor.Search.Collections
{
    enum ResizingWindow
    {
        None,
        Left,
        Right,
        Bottom,
        Gripper
    }

    [Icon("Icons/QuickSearch/ListView.png")]
    [Overlay(typeof(SceneView), "Collections", defaultLayout: false)]
    class SearchCollectionOverlay : Overlay, ISearchCollectionHostView, IHasCustomMenu
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

        static int RESIZER_CONTROL_ID = "SearchCollectionOverlayResizer".GetHashCode();

        IMGUIContainer m_CollectionContainer;
        ResizingWindow m_Resizing = ResizingWindow.None;
        [SerializeField] SearchCollectionView m_CollectionView;
        
        public string name => "overlay";
        public bool overlay => true;
        public bool docked => false;

        public SearchCollectionOverlay()
        {
            layout = Layout.Panel;

            m_CollectionView = new SearchCollectionView();
            m_CollectionView.Initialize(this);
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
            HandleOverlayResize(evt);

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

            var hashForSearchField = "CollectionsSearchField".GetHashCode();
            var searchFieldControlID = GUIUtility.GetControlID(hashForSearchField, FocusType.Passive, searchTextRect);
            m_CollectionView.searchText = EditorGUI.ToolbarSearchField(
                searchFieldControlID,
                searchTextRect,
                m_CollectionView.searchText,
                EditorStyles.toolbarSearchField,
                string.IsNullOrEmpty(m_CollectionView.searchText) ? GUIStyle.none : EditorStyles.toolbarSearchFieldCancelButton);

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

        private void HandleOverlayResize(Event evt)
        {
            if (evt.type == EventType.MouseUp && m_Resizing != ResizingWindow.None)
            {
                GUIUtility.hotControl = 0;
                m_Resizing = ResizingWindow.None;
            }
            else if (evt.type == EventType.MouseDrag && GUIUtility.hotControl == RESIZER_CONTROL_ID)
            {
                switch (m_Resizing)
                {
                    case ResizingWindow.Left:
                        var mousePositionUnclipped = Utils.Unclip(new Rect(evt.mousePosition, Vector2.zero)).position;
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

        public void Close()
        {
            displayed = false;
        }

        public void Repaint()
        {
            // TODO:
        }
    }
}
#endif
