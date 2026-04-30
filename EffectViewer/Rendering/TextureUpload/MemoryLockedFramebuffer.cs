using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Platform;

namespace EffectViewer.Rendering.TextureUpload
{
    internal sealed class MemoryLockedFramebuffer : ILockedFramebuffer
    {
        private readonly GCHandle _handle;

        public IntPtr Address { get; }
        public PixelSize Size { get; }
        public int RowBytes { get; }
        public Vector Dpi { get; } = new(96, 96);
        public PixelFormat Format { get; } = PixelFormat.Rgba8888;
        public AlphaFormat AlphaFormat { get; } = AlphaFormat.Unpremul;

        public MemoryLockedFramebuffer(byte[] pixels, int width, int height)
        {
            Size = new PixelSize(width, height);
            RowBytes = width * 4;
            _handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            Address = _handle.AddrOfPinnedObject();
        }

        public void Dispose()
        {
            if (_handle.IsAllocated)
            {
                _handle.Free();
            }
        }
    }
}
