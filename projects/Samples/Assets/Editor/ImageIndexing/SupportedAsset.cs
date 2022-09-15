using System;

namespace UnityEditor.Search
{
    struct SupportedImageType
    {
        public Type assetType;
        public ImageType imageType;
        public string assetDatabaseQuery;
        public Func<string, ITextureAsset> textureAssetCreator;
    }
}
