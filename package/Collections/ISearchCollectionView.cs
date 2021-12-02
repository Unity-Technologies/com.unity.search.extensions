#if UNITY_2021_2_OR_NEWER
using System.Collections.Generic;

namespace UnityEditor.Search.Collections
{
    interface ISearchCollectionView
    {
        bool overlay { get; }
        string searchText { get; set; }
        ICollection<SearchCollection> collections { get; }
        
        void OpenContextualMenu();
        void AddCollectionMenus(GenericMenu menu);
        void SaveCollections();
    }
}
#endif
