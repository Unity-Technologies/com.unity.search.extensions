using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.Search
{
    static class DependencyTableUtilities
    {
        public static void ExpandUsesItems(DependencyTableView tableView, IEnumerable<SearchItem> items)
        {
            DependencyViewerFlags flags = DependencyViewerFlags.Uses | DependencyViewerFlags.ShowSceneRefs;
            foreach (var i in items)
                ExpandItem(flags, tableView, i);
        }

        public static void ExpandUsedByItems(DependencyTableView tableView, IEnumerable<SearchItem> items)
        {
            DependencyViewerFlags flags = DependencyViewerFlags.UsedBy | DependencyViewerFlags.ShowSceneRefs;
            foreach (var i in items)
                ExpandItem(flags, tableView, i);
        }

        public static void ExpandItem(DependencyViewerFlags flags, DependencyTableView tableView, SearchItem item)
        {
            var treeViewItem = tableView.table.GetTreeViewItem(item);
            if (treeViewItem.hasChildren)
                return;

            var itemObj = item.ToObject();
            if (!itemObj || itemObj == null)
                return;

            var showSceneRefs = flags.HasFlag(DependencyViewerFlags.ShowSceneRefs);
            var desc = flags.HasFlag(DependencyViewerFlags.Uses) ? Dependency.CreateUsesContext(new[] { itemObj }, showSceneRefs) : Dependency.CreateUsedByContext(new[] { itemObj }, showSceneRefs);
            if (!desc.isValid)
                return;

            var ctx = desc.CreateContext();
            SearchService.Request(ctx, (_ctx, items) =>
            {
                tableView.table.AddItems(items, item);
            });
        }
    }
}