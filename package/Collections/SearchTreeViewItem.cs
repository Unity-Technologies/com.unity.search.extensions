using System;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace UnityEditor.Search.Collections
{
    class SearchTreeViewItem : TreeViewItem
    {
        static int s_NextId = 10000;

        string m_Label;
        SearchItem m_SearchItem;
        public SearchItem item => m_SearchItem;

        protected readonly SearchCollectionTreeView m_TreeView;

        public SearchTreeViewItem(SearchCollectionTreeView treeView)
            : base(s_NextId++, 0)
        {
            m_TreeView = treeView ?? throw new ArgumentNullException(nameof(treeView));
            m_SearchItem = new SearchItem(Guid.NewGuid().ToString("N"));
        }

        public SearchTreeViewItem(SearchCollectionTreeView treeView, SearchContext context, SearchItem item)
            : this(treeView)
        {
            m_SearchItem = item;
            displayName = item.label ?? item.id;
            icon = null;
        }

        public virtual string GetLabel()
        {
            if (m_Label == null)
            {
                var label = item.GetDescription(item.context);
                if (!string.IsNullOrEmpty(label))
                {
                    var p = label.LastIndexOf('/');
                    if (p != -1)
                        label = label.Substring(p+1);
                }
                displayName = m_Label = label ?? string.Empty;
            }
            return m_Label;
        }

        public virtual Texture2D GetThumbnail()
        {
            if (!icon)
                icon = item.GetPreview(item.context, new Vector2(24, 24), FetchPreviewOptions.Normal, cacheThumbnail: false);
            return icon ?? Icons.quicksearch;
        }

        public virtual void Select()
        {
            var go = m_SearchItem.ToObject<GameObject>();
            if (go)
                Selection.activeGameObject = go;
            else
                m_SearchItem.provider?.trackSelection?.Invoke(m_SearchItem, m_SearchItem.context);
        }

        public virtual void Open()
        {
            var currentSelection = new[] { m_SearchItem };
            var defaultAction = m_SearchItem.provider?.actions.FirstOrDefault(a => a.enabled?.Invoke(currentSelection) ?? true);
            ExecuteAction(defaultAction, new [] { m_SearchItem });
        }

        public virtual void OpenContextualMenu()
        {
            var menu = new GenericMenu();
            var selectedItems = m_TreeView.GetSelectedItems();
            var currentSelection = selectedItems.Contains(this) ? m_TreeView.GetSelectedItems().Cast<SearchTreeViewItem>().Select(e => e.item).ToArray() : new [] { m_SearchItem };
            foreach (var action in m_SearchItem.provider.actions.Where(a => a.enabled(currentSelection)))
            {
                var itemName = !string.IsNullOrWhiteSpace(action.content.text) ? action.content.text : action.content.tooltip;
                menu.AddItem(new GUIContent(itemName, action.content.image), false, () => ExecuteAction(action, currentSelection, true));
            }

            menu.ShowAsContext();
        }

        private void ExecuteAction(SearchAction action, SearchItem[] currentSelection, bool refresh = false)
        {
            if (action == null)
                return;
            if (action.handler != null)
            {
                action.handler(m_SearchItem);
                if (refresh)
                    Refresh();
            }
            else if (action.execute != null)
            {
                action.execute(currentSelection);
                if (refresh)
                    Refresh();
            }
        }

        void Refresh()
        {
            if (parent is SearchCollectionTreeViewItem ctvi)
                Utils.CallDelayed(() => ctvi.Refresh(), 2d);
        }

        public virtual bool CanStartDrag()
        {
            return m_SearchItem.provider?.startDrag != null;
        }

        public virtual UnityEngine.Object GetObject()
        {
            return m_SearchItem.provider?.toObject(m_SearchItem, typeof(UnityEngine.Object));
        }
    }
}
