using System;
using UnityEngine;

namespace UnityEditor.Search
{
    class ImagePixels
    {
        public int width;
        public int height;
        public Color[] pixels;

        public ImagePixels(Texture2D texture)
        {
            width = texture.width;
            height = texture.height;
            pixels = TextureUtils.GetPixels(texture);
        }

        public ImagePixels(int width, int height, Color[] pixels)
        {
            this.width = width;
            this.height = height;
            this.pixels = pixels;
        }
    }
}
