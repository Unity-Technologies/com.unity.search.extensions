using System;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace UnityEditor.Search.Collections
{
    class SearchTreeViewItem : TreeViewItem
    {
        static int s_NextId = 10000;

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
            item.options |= SearchItemOptions.Compacted;
            displayName = item.GetLabel(context, stripHTML: true);
            icon = item.GetThumbnail(context, cacheThumbnail: false);
        }

        public virtual void Select()
        {
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
            var currentSelection = new[] { m_SearchItem };
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

        public virtual bool DrawRow(Rect rowRect)
        {
            return false;
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

        public UnityEngine.Object GetObject()
        {
            return m_SearchItem.provider?.toObject(m_SearchItem, typeof(UnityEngine.Object));
        }
    }
}
