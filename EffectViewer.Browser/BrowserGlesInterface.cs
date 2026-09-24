using System;
using System.Runtime.InteropServices;
using EffectViewer.Rendering.Gl;

namespace EffectViewer.Browser;

// These are Emscripten GLES function pointers, not delegates to JS WebGL objects.
internal sealed unsafe partial class BrowserGlesInterface : ProcAddressEffectGlInterface
{
    internal const uint Texture2D = 0x0DE1;
    internal const uint Framebuffer = 0x8D40;
    internal const uint Rgba8 = 0x8058;
    internal const uint DrawFramebuffer = 0x8CA9;
    internal const uint ReadFramebuffer = 0x8CA8;
    internal const uint DrawFramebufferBinding = 0x8CA6;
    internal const uint ReadFramebufferBinding = 0x8CAA;

    private readonly delegate* unmanaged<int, uint*, void> _genFramebuffers;
    private readonly delegate* unmanaged<int, uint*, void> _deleteFramebuffers;
    private readonly delegate* unmanaged<uint, uint, uint, uint, int, void> _framebufferTexture2D;
    private readonly delegate* unmanaged<uint, uint> _checkFramebufferStatus;
    private readonly delegate* unmanaged<uint, void> _disable;
    private readonly delegate* unmanaged<byte, byte, byte, byte, void> _colorMask;
    private readonly delegate* unmanaged<uint, void> _blendEquation;
    private readonly delegate* unmanaged<uint, uint, void> _bindSampler;

    public BrowserGlesInterface() : base(EffectGlApi.WebGl, "WebGL2 (Avalonia compositor)", GetProcAddress)
    {
        _genFramebuffers = (delegate* unmanaged<int, uint*, void>)GetProcAddress("glGenFramebuffers");
        _deleteFramebuffers = (delegate* unmanaged<int, uint*, void>)GetProcAddress("glDeleteFramebuffers");
        _framebufferTexture2D = (delegate* unmanaged<uint, uint, uint, uint, int, void>)GetProcAddress("glFramebufferTexture2D");
        _checkFramebufferStatus = (delegate* unmanaged<uint, uint>)GetProcAddress("glCheckFramebufferStatus");
        _disable = (delegate* unmanaged<uint, void>)GetProcAddress("glDisable");
        _colorMask = (delegate* unmanaged<byte, byte, byte, byte, void>)GetProcAddress("glColorMask");
        _blendEquation = (delegate* unmanaged<uint, void>)GetProcAddress("glBlendEquation");
        _bindSampler = (delegate* unmanaged<uint, uint, void>)GetProcAddress("glBindSampler");
    }

    [LibraryImport("libSkiaSharp", EntryPoint = "eglGetProcAddress", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr EglGetProcAddress(string name);

    private static IntPtr GetProcAddress(string name)
    {
        IntPtr address = EglGetProcAddress(name);
        return address != IntPtr.Zero ? address : throw new InvalidOperationException($"Missing GLES entry point: {name}");
    }

    public uint CreateFramebuffer(uint texture)
    {
        uint framebuffer;
        _genFramebuffers(1, &framebuffer);
        BindFramebuffer(Framebuffer, framebuffer);
        _framebufferTexture2D(Framebuffer, 0x8CE0 /* COLOR_ATTACHMENT0 */, Texture2D, texture, 0);
        uint status = _checkFramebufferStatus(Framebuffer);
        if (status != 0x8CD5 /* FRAMEBUFFER_COMPLETE */)
        {
            DeleteFramebuffer(framebuffer);
            throw new InvalidOperationException($"Incomplete WebGL2 framebuffer: 0x{status:X}");
        }
        return framebuffer;
    }

    public void DeleteFramebuffer(uint framebuffer) => _deleteFramebuffers(1, &framebuffer);

    public void PrepareForRendering()
    {
        // Skia leaves its GL state current. Establish every state our renderer relies on.
        _disable(0x0B71); // DEPTH_TEST
        _disable(0x0B44); // CULL_FACE
        _disable(0x0C11); // SCISSOR_TEST
        _disable(0x0B90); // STENCIL_TEST
        _disable(0x8C89); // RASTERIZER_DISCARD
        _colorMask(1, 1, 1, 1);
        _blendEquation(0x8006); // FUNC_ADD
        ActiveTexture(0x84C0); // TEXTURE0
        _bindSampler(0, 0);
        BindBuffer(0x88EC, 0); // PIXEL_UNPACK_BUFFER
        PixelStorei(0x0CF2, 0); // UNPACK_ROW_LENGTH
        PixelStorei(0x0CF3, 0); // UNPACK_SKIP_ROWS
        PixelStorei(0x0CF4, 0); // UNPACK_SKIP_PIXELS
    }
}
