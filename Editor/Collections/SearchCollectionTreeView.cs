using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace UnityEditor.Search.Collections
{
    class SearchCollectionTreeView : TreeView
    {
        readonly ISearchCollectionView searchView;
        public ICollection<SearchCollection> collections => searchView.collections;

        public SearchCollectionTreeView(TreeViewState treeViewState, ISearchCollectionView searchView)
            : base(treeViewState, new SearchCollectionColumnHeader(searchView))
        {
            this.searchView = searchView ?? throw new ArgumentNullException(nameof(searchView));
            //showAlternatingRowBackgrounds = true;

            Reload();
            EditorApplication.delayCall += () => multiColumnHeader.ResizeToFit();
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem { id = int.MinValue, depth = -1, displayName = "Root", children = new List<TreeViewItem>() };
            foreach (var coll in collections)
                root.AddChild(new SearchCollectionTreeViewItem(this, coll));
            return root;
        }

        protected override IList<TreeViewItem> BuildRows(TreeViewItem rowItem)
        {
            EditorApplication.update -= DelayedUpdateCollections;
            return base.BuildRows(rowItem);
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var evt = Event.current;

            if (args.item is SearchCollectionTreeViewItem ctvi)
            {
				var c = ctvi.collection.color;
                if (c.a == 0f)
                    c = new Color(80 / 255f, 80 / 255f, 80 / 255f, 1f);
                if (evt.type == EventType.Repaint && c.a != 0f)
                    GUI.DrawTexture(args.rowRect, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(c.r, c.g, c.b, 1.0f), 0f, 0f);

                var buttonRect = args.rowRect;
                buttonRect.xMin = buttonRect.xMax - 24f;
                GUI.Button(buttonRect, EditorGUIUtility.IconContent("scenepicking_pickable"), "IconButton");

                buttonRect.x -= 20f;
                GUI.Button(buttonRect, EditorGUIUtility.IconContent("SceneViewCamera"), "IconButton");

                if (evt.type == EventType.MouseDown && evt.button == 1 && args.rowRect.Contains(evt.mousePosition))
                { 
                    ctvi.OpenContextualMenu();
                    evt.Use();
                }
            }

            base.RowGUI(args);
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            base.SelectionChanged(selectedIds);
            if (selectedIds.Count == 0)
                return;

            if (FindItem(selectedIds.Last(), rootItem) is SearchTreeViewItem stvi)
                stvi.Select();
        }

        protected override void DoubleClickedItem(int id)
        {
            if (FindItem(id, rootItem) is SearchTreeViewItem stvi)
                stvi.Open();
        }

        protected override void ContextClicked()
        {
            OpenContextualMenu(() => searchView.OpenContextualMenu());
        }

        protected override void ContextClickedItem(int id)
        {
            if (FindItem(id, rootItem) is SearchTreeViewItem stvi)
                OpenContextualMenu(() => stvi.OpenContextualMenu());
        }

        private void OpenContextualMenu(Action handler)
        {
            handler();
            Event.current.Use();
        }

        protected override bool CanStartDrag(CanStartDragArgs args)
        {
            if (args.draggedItem is SearchTreeViewItem stvi)
                return stvi.CanStartDrag();
            return false;
        }

        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            var items = args.draggedItemIDs.Select(id => FindItem(id, rootItem) as SearchTreeViewItem).Where(i => i != null);
            var selectedObjects = items.Select(e => e.GetObject()).Where(o => o).ToArray();
            if (selectedObjects.Length == 0)
                return;
            var paths = selectedObjects.Select(i => AssetDatabase.GetAssetPath(i)).ToArray();
            Utils.StartDrag(selectedObjects, paths, string.Join(", ", items.Select(e => e.displayName)));
        }

        public void Add(SearchCollection newCollection)
        {
            collections.Add(newCollection);
            rootItem.AddChild(new SearchCollectionTreeViewItem(this, newCollection));
            BuildRows(rootItem);
            searchView.SaveCollections();
        }

        public void Remove(SearchCollectionTreeViewItem collectionItem, SearchCollection collection)
        {
            collections.Remove(collection);
            rootItem.children.Remove(collectionItem);
            BuildRows(rootItem);
            searchView.SaveCollections();
        }

        public void UpdateCollections()
        {
            EditorApplication.update -= DelayedUpdateCollections;
            EditorApplication.update += DelayedUpdateCollections;
        }

        private void DelayedUpdateCollections()
        {
            EditorApplication.update -= DelayedUpdateCollections;
            BuildRows(rootItem);
            Repaint();
        }
    }
}