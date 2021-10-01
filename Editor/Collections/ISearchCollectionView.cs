using System.Collections.Generic;

namespace UnityEditor.Search.Collections
{
    interface ISearchCollectionView
    {
        string searchText { get; set; }
        ICollection<SearchCollection> collections { get; }

        void OpenContextualMenu();
        void AddCollectionMenus(GenericMenu menu);
        void SaveCollections();
    }
}
