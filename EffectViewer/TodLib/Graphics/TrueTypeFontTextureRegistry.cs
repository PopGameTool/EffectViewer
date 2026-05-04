using System;
using System.Collections.Generic;

namespace EffectViewer.TodLib.Graphics
{
    internal static class TrueTypeFontTextureRegistry
    {
        private static readonly object sLock = new();
        private static readonly Dictionary<string, TrueTypeFontTextureSnapshot> sTextures = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> sRevisions = new(StringComparer.Ordinal);

        public static int Set(string id, int width, int height, byte[] rgbaPixels)
        {
            if (string.IsNullOrWhiteSpace(id) ||
                width <= 0 ||
                height <= 0 ||
                rgbaPixels is null ||
                rgbaPixels.Length < width * height * 4)
            {
                return 0;
            }

            byte[] copy = new byte[width * height * 4];
            Array.Copy(rgbaPixels, copy, copy.Length);
            lock (sLock)
            {
                int revision = sRevisions.GetValueOrDefault(id) + 1;
                sRevisions[id] = revision;
                sTextures[id] = new TrueTypeFontTextureSnapshot(width, height, copy, revision);
                return revision;
            }
        }

        public static bool TryGet(string id, out TrueTypeFontTextureSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                snapshot = null;
                return false;
            }

            lock (sLock)
            {
                return sTextures.TryGetValue(id, out snapshot);
            }
        }

        public static void Remove(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            lock (sLock)
            {
                sTextures.Remove(id);
            }
        }
    }

    internal sealed class TrueTypeFontTextureSnapshot
    {
        public TrueTypeFontTextureSnapshot(int width, int height, byte[] rgbaPixels, int revision)
        {
            Width = width;
            Height = height;
            RgbaPixels = rgbaPixels;
            Revision = revision;
        }

        public int Width { get; }
        public int Height { get; }
        public byte[] RgbaPixels { get; }
        public int Revision { get; }
    }
}
