using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.Search
{
    interface ITextureAsset
    {
        public string path { get; }
        public Texture2D texture { get; }
        public bool valid { get; }
        public ImageType imageType { get; }
    }

    readonly struct TextureAsset : ITextureAsset
    {
        public string path { get; }
        public Texture2D texture { get; }
        public bool valid => texture != null;
        public ImageType imageType => ImageType.Texture2D;

        public TextureAsset(string path)
        {
            this.path = path;
            this.texture = AssetDatabase.LoadMainAssetAtPath(path) as Texture2D;
        }

        public TextureAsset(string path, Texture2D texture)
        {
            this.path = path;
            this.texture = texture;
        }
    }

    readonly struct SpriteAsset : ITextureAsset
    {
        public string path { get; }
        public Texture2D texture => m_Sprite?.texture;
        public ImageType imageType => ImageType.Sprite;
        readonly Sprite m_Sprite;

        public bool valid => texture != null;

        public SpriteAsset(string path)
        {
            this.path = path;
            m_Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public SpriteAsset(string path, Sprite texture)
        {
            this.path = path;
            m_Sprite = texture;
        }
    }

    class TextureAssetComparer : IEqualityComparer<ITextureAsset>
    {
        public bool Equals(ITextureAsset x, ITextureAsset y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            return x.path == y.path && x.imageType == y.imageType;
        }

        public int GetHashCode(ITextureAsset obj)
        {
            return HashCode.Combine(obj.path, obj.imageType);
        }
    }
}
