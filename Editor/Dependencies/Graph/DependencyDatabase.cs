using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.Search
{
    enum DependencyType : uint
    {
        File = 0x01,
        Folder,
        Asset,
        Object,
        BuiltIn,
        External = 0xF000,
        Unknown = 0xF0000000
    };

    enum DependencyLink
    {
        Direct = 1,
        Weak
    };

	class DependencyItem
	{
		public readonly int id;
		public readonly string path;

		System.Type m_Type;
		Texture m_Preview;
		int? m_InstanceID;

		public System.Type type
		{
			get
			{
				if (m_Type == null)
					m_Type = AssetDatabase.GetMainAssetTypeAtPath(path);
				return m_Type;
			}
		}

		public Texture preview
		{
			get
			{
				if (!m_Preview)
					m_Preview = AssetDatabase.GetCachedIcon(path);
				return m_Preview;
			}
		}

		public int instanceID
		{
			get
			{
				if (!m_InstanceID.HasValue)
					m_InstanceID = Utils.GetMainAssetInstanceID(path);
				return m_InstanceID.Value;
			}
		}

		public DependencyItem(int id, string path)
		{
			this.id = id;
			this.path = path;
		}
	}

	class DependencyDatabase
    {
		public DependencyDatabase()
		{
			m_NextId = 1;
			m_IdByPath = new Dictionary<string, int>();
			m_Items = new Dictionary<int, DependencyItem>();
		}

		volatile int m_NextId;
		Dictionary<string, int> m_IdByPath;
		Dictionary<int, DependencyItem> m_Items;

        // Resource APIs
        //
        public int GetResourceID(int index)
		{
			Debug.Log($"GetResourceID({index})");
			return 0;
		}

        public string GetResourceName(int id)
		{
			if (!m_Items.TryGetValue(id, out var di))
				return null;
			return di.path;
		}

        public DependencyType GetResourceType(int id)
		{
			Debug.Log($"GetResourceType({id})");
			return DependencyType.Asset;
		}

        public string GetResourceTypeName(int id)
		{
			if (!m_Items.TryGetValue(id, out var di))
				return null;
			return di.type?.Name;
		}

        public int[] GetResourceDependencies(int id)
		{
			if (!m_Items.TryGetValue(id, out var di))
				return new int[0];
			return SearchItemIds("from", di.path).ToArray();
		}

        public int[] GetResourceReferences(int id)
		{
			if (!m_Items.TryGetValue(id, out var di))
				return new int[0];
			return SearchItemIds("ref", di.path).ToArray();
		}

		ISet<int> SearchItemIds(in string op, in string assetPath)
		{
			return new HashSet<int>(SearchGUIDs(op, assetPath)
				.Select(guid => FindResourceByName(AssetDatabase.GUIDToAssetPath(guid))));
		}

		IEnumerable<string> SearchGUIDs(string op, string assetPath)
		{
			var depProvider = SearchService.GetProvider("dep");
			using (var context = SearchService.CreateContext(depProvider, $"{op}=\"{assetPath}\""))
			using (var request = SearchService.Request(context, SearchFlags.Synchronous))
			{
				foreach (var r in request)
				{
					if (r == null)
						continue;
					yield return r.id;
				}
			}
		}

        public int[] GetWeakDependencies(int id)
		{
			return new int[0];
		}

        public Texture GetResourcePreview(int id)
		{
			if (!m_Items.TryGetValue(id, out var di))
				return null;
			return di.preview;
		}

        // Find APIs
        //
        public int FindResourceByName(in string path)
		{
			if (m_IdByPath.TryGetValue(path, out var id))
				return id;

			var nextId = m_NextId++;
			m_IdByPath[path] = nextId;
			var di = new DependencyItem(nextId, path);
			m_Items[di.id] = di;
			return di.id;
		}
    }
}
