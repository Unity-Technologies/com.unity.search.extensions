#if UNITY_2021_2_OR_NEWER
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using System.Reflection;

namespace UnityEditor.Search.Collections
{
    class SearchCollectionTreeView : TreeView
    {
        static class InnerStyles
        {
            public static GUIStyle FromUSS(string name)
            {
                return FromUSS(GUIStyle.none, name);
            }

            private static MethodInfo s_FromUSSMethod;
            public static GUIStyle FromUSS(GUIStyle @base, string name)
            {
                if (s_FromUSSMethod == null)
                {
                    Assembly assembly = typeof(UnityEditor.EditorStyles).Assembly;
                    var type = assembly.GetTypes().First(t => t.FullName == "UnityEditor.StyleSheets.GUIStyleExtensions");
                    s_FromUSSMethod = type.GetMethod("FromUSS", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(GUIStyle), typeof(string), typeof(string), typeof(GUISkin) }, null);
                }
                string ussInPlaceStyleOverride = null;
                GUISkin srcSkin = null;
                return (GUIStyle)s_FromUSSMethod.Invoke(null, new object[] { @base, name, ussInPlaceStyleOverride, srcSkin });
            }

            private static readonly RectOffset paddingNone = new RectOffset(0, 0, 0, 0);

            public static readonly GUIStyle treeItemLabel = FromUSS(new GUIStyle()
            {
                wordWrap = false,
                stretchWidth = false,
                stretchHeight = false,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Overflow,
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0)
            }, "quick-search-tree-view-item");

            public static readonly GUIStyle actionButton = new GUIStyle("IconButton")
            {
                imagePosition = ImagePosition.ImageOnly
            };

            public static readonly GUIStyle itemLabel = new GUIStyle(EditorStyles.label)
            {
                name = "quick-search-item-label",
                richText = true,
                wordWrap = false,
                margin = new RectOffset(8, 4, 4, 2),
                padding = paddingNone
            };

            public static readonly RectOffset itemMargins = new RectOffset(2, 2, 1, 1);
        }

        readonly ISearchCollectionView searchView;
        public ICollection<SearchCollection> collections => searchView.collections;

        public SearchCollectionTreeView(TreeViewState treeViewState, ISearchCollectionView searchView)
            : base(treeViewState, searchView.overlay ? null : new SearchCollectionColumnHeader(searchView))
        {
            this.searchView = searchView ?? throw new ArgumentNullException(nameof(searchView));
            showAlternatingRowBackgrounds = false;
            showBorder = false;
            baseIndent = -10f;
            if (searchView.overlay)
            {
                rowHeight = 22f;

                var controller = typeof(TreeView).GetProperty("controller", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this);
                controller.GetType().GetProperty("scrollViewStyle", BindingFlags.Instance | BindingFlags.Public).SetValue(controller, GUIStyle.none);
                //drawSelection = false;
            }
            else
            {
                rowHeight = 22f;
                EditorApplication.delayCall += () => multiColumnHeader.ResizeToFit();
            }

            Reload();
        }

        public IList<TreeViewItem> GetSelectedItems()
        {
            return FindRows(GetSelection());
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
            if (args.isRenaming)
            {
                base.RowGUI(args);
                return;
            }

            var evt = Event.current;
            var rowRect = InnerStyles.itemMargins.Remove(args.rowRect);
            var hovered = rowRect.Contains(evt.mousePosition);

            if (args.item is SearchCollectionTreeViewItem ctvi)
                DrawCollectionItem(rowRect, args, hovered, evt, ctvi);
            else if (args.item is SearchTreeViewItem stvi)
                DrawSearchItem(rowRect, args, evt, stvi);
            else
                base.RowGUI(args);

            HandleItemContextualMenu(evt, hovered, args.item as SearchTreeViewItem);
        }

        private void HandleItemContextualMenu(Event evt, bool hovered, in SearchTreeViewItem tvi)
        {
            if (evt.type != EventType.MouseDown || evt.button != 1 || !hovered || tvi == null)
                return;

            tvi.OpenContextualMenu();
            evt.Use();
        }

        private void DrawSearchItem(Rect rowRect, RowGUIArgs args, Event evt, SearchTreeViewItem stvi)
        {
            rowRect.xMin += 6f;
            DrawItem(rowRect, args, evt, stvi, InnerStyles.treeItemLabel);
        }

        private void DrawCollectionItem(in Rect rowRect, in RowGUIArgs args, in bool hovered, in Event evt, SearchCollectionTreeViewItem ctvi)
        {
            if (evt.type == EventType.Repaint)
            {
                var c = ctvi.collection.color;
                if (c.a == 0f)
                    c = new Color(68 / 255f, 68 / 255f, 68 / 255f, 0.7f);
                c = new Color(c.r, c.g, c.b, hovered ? 0.8f : 0.6f);
                GUI.DrawTexture(rowRect, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, false, 0f, c, 0f, 2f);
                DrawItem(rowRect, args, evt, ctvi, InnerStyles.itemLabel);
                if (args.selected)
                {
                    var borderRect = rowRect;
                    borderRect.xMin -= 1f;
                    borderRect.xMax += 1f;
                    borderRect.yMin -= 1f;
                    borderRect.yMax += 1f;
                    GUI.DrawTexture(borderRect, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(70 / 255f, 96 / 255f, 124 / 255f, 1.0f), 2f, 2f);
                }
            }

            if (hovered)
                ctvi.DrawActions(rowRect, InnerStyles.actionButton);
        }

        internal void SaveCollections()
        {
            searchView.SaveCollections();
        }

        private void DrawItem(Rect rowRect, in RowGUIArgs args, in Event evt, in SearchTreeViewItem stvi, in GUIStyle style)
        {
            if (evt.type == EventType.Repaint)
            {
                rowRect.xMin += 2f;
                var itemContent = EditorGUIUtility.TrTextContentWithIcon(stvi.GetLabel(), stvi.GetThumbnail());
                style.Draw(rowRect, itemContent,
                    isHover: rowRect.Contains(evt.mousePosition), isActive: args.selected, on: false, hasKeyboardFocus: false);
            }
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
            #if USE_SEARCH_EXTENSION_API                    
            SearchUtils.StartDrag(selectedObjects, paths, string.Join(", ", items.Select(e => e.displayName)));
            #else
            Utils.StartDrag(selectedObjects, paths, string.Join(", ", items.Select(e => e.displayName)));
            #endif
        }

        protected override bool CanRename(TreeViewItem item)
        {
            return item is SearchCollectionTreeViewItem;
        }

        protected override void RenameEnded(RenameEndedArgs args)
        {
            if (args.acceptedRename && FindItem(args.itemID, rootItem) is SearchCollectionTreeViewItem ctvi)
            {
                ctvi.displayName = ctvi.collection.name = args.newName;
                SaveCollections();
            }
            else
                base.RenameEnded(args);
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
            if (collections.Remove(collection) && rootItem.children.Remove(collectionItem))
            {
                BuildRows(rootItem);
                searchView.SaveCollections();
            }
        }

        public void Remove(in TreeViewItem item)
        {
            if (item.parent is SearchCollectionTreeViewItem ctvi && ctvi.children.Remove(item))
            {
                if (item is SearchObjectTreeViewItem otvi)
                    ctvi.collection.RemoveObject(otvi.GetObject());
                BuildRows(rootItem);
                searchView.SaveCollections();
            }
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

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            if (DragAndDrop.objectReferences == null || DragAndDrop.objectReferences.Length == 0)
                return DragAndDropVisualMode.Rejected;
            if (args.parentItem is SearchCollectionTreeViewItem ctvi)
            {
                if (args.performDrop)
                {
                    ctvi.AddObjectsToTree(DragAndDrop.objectReferences);
                    Repaint();
                }
                return DragAndDropVisualMode.Copy;
            }
            return DragAndDropVisualMode.Rejected;
        }
    }
}
#endif
