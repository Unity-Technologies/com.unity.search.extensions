using System;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace UnityEditor.Search.Collections
{
    class SearchObjectTreeViewItem : SearchTreeViewItem
    {
        UnityEngine.Object m_Object;
        public SearchObjectTreeViewItem(SearchCollectionTreeView treeView, UnityEngine.Object obj)
            : base(treeView)
        {
            m_Object = obj;
            displayName = m_Object.name;
        }

        public override string GetLabel()
        {
            return displayName;
        }

        public override Texture2D GetThumbnail()
        {
            return (icon = AssetPreview.GetMiniThumbnail(m_Object));
        }

        public override void Select()
        {
            EditorGUIUtility.PingObject(m_Object);
            if (m_Object is GameObject go)
                Selection.activeGameObject = go;
        }

        public override void Open()
        {
            Selection.activeObject = m_Object;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();
        }

        public override bool CanStartDrag()
        {
            return true;
        }

        public override UnityEngine.Object GetObject()
        {
            return m_Object;
        }

        public override void OpenContextualMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Remove"), false, RemoveItem);
            menu.ShowAsContext();
        }

        private void RemoveItem()
        {
            if (m_TreeView.GetSelectedItems().Contains(this))
            {
                foreach (var r in m_TreeView.GetSelectedItems())
                    m_TreeView.Remove(r);
            }
            else
                m_TreeView.Remove(this);
            m_TreeView.SaveCollections();
        }
    }
}
