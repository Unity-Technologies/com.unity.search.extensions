using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;

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

        public static void ExpandDependencies(DependencyViewerFlags flags, IEnumerable<SearchItem> items, int depth, Action<SearchContext, IEnumerable<SearchItem>, int> onNewItems, Action<SearchContext, IEnumerable<SearchItem>, int> onDone)
        {
            ExpandDependencies(flags, items.ToHashSet(), new HashSet<SearchItem>(), 0, depth, onNewItems, onDone);
        }

        public static void ExpandDependencies(DependencyViewerFlags flags, HashSet<SearchItem> toProcessItems, HashSet<SearchItem> processedItems, 
            int currentDepth, int requestedDepth, Action<SearchContext, IEnumerable<SearchItem>, int> onNewItems, Action<SearchContext, IEnumerable<SearchItem>, int> onDone)
        {
            var objects = toProcessItems.Select(item => item.ToObject()).Where(o => o);
            var showSceneRefs = flags.HasFlag(DependencyViewerFlags.ShowSceneRefs);
            var desc = Dependency.CreateUsesContext(objects, showSceneRefs);
            var ctx = desc.CreateContext();
            currentDepth++;
            SearchService.Request(ctx, (_ctx, items) =>
            {
                processedItems.UnionWith(toProcessItems);
                var depLevelItemSet = new HashSet<SearchItem>(items);
                var newItems = depLevelItemSet.Except(processedItems);
                foreach (var item in depLevelItemSet)
                {
                    item.SetField("depth", currentDepth);
                }
                if (onNewItems != null)
                    onNewItems(ctx, newItems, currentDepth);
                if (currentDepth < requestedDepth && newItems.Any())
                {
                    ExpandDependencies(flags, newItems.ToHashSet(), processedItems, currentDepth, requestedDepth, onNewItems, onDone);
                }
                else
                {
                    if (onDone != null)
                        onDone(ctx, processedItems, currentDepth);
                }
            });
        }
    }
}