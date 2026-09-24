using System;
using System.Numerics;
using Avalonia;
using Avalonia.OpenGL;
using Avalonia.Skia;
using EffectViewer.Rendering;
using EffectViewer.Rendering.OpenGl;
using EffectViewer.Rendering.TextureUpload;
using SkiaSharp;

namespace EffectViewer.Browser;

// Retained by recorded draw operations until Avalonia releases them.
// Browser rendering and control updates run on the same WASM thread.
internal sealed class BrowserWebGlRenderSession
{
    private int _references = 1;
    private IGlContext? _context;
    private GRContext? _grContext;
    private BrowserGlesInterface? _gl;
    private OpenGlRenderer _renderer = new();
    private uint _texture;
    private uint _framebuffer;
    private PixelSize _size;

    public void Retain() => _references++;
    public void Release()
    {
        if (--_references == 0)
            DisposeResources();
    }

    public void Draw(ISkiaSharpApiLease lease, RenderFrame frame, ITextureSource textures,
        PixelSize size, Rect bounds, float zoom, Vector2 pan, Vector4 clear,
        Vector4? checkerboard, int cellSize)
    {
        GRContext grContext = lease.GrContext
            ?? throw new InvalidOperationException("Effect preview requires Avalonia's WebGL2 renderer.");
        using (ISkiaSharpPlatformGraphicsApiLease platform = lease.TryLeasePlatformGraphicsApi()
            ?? throw new InvalidOperationException("Avalonia did not expose its graphics context."))
        {
            if (platform.Context is not IGlContext context || context.Version.Major < 3)
                throw new InvalidOperationException("Effect preview requires a WebGL2 / GLES3 context.");
            if (!ReferenceEquals(_context, context) || !ReferenceEquals(_grContext, grContext))
            {
                DisposeResources();
                _context = context;
                _grContext = grContext;
                _gl = new BrowserGlesInterface();
            }

            BrowserGlesInterface gl = _gl!;
            gl.GetIntegerv(BrowserGlesInterface.DrawFramebufferBinding, out int drawFramebuffer);
            gl.GetIntegerv(BrowserGlesInterface.ReadFramebufferBinding, out int readFramebuffer);
            try
            {
                gl.PrepareForRendering();
                // Release old GPU textures before changing the source.
                if (!ReferenceEquals(_renderer.TextureSource, textures))
                {
                    _renderer.Deinitialize();
                    _renderer.TextureSource = textures;
                }
                if (!_renderer.IsInitialized)
                    _renderer.Initialize(gl);
                EnsureOutput(size);
                _renderer.ViewZoom = zoom;
                _renderer.ViewPan = pan;
                _renderer.Render(frame, (int)_framebuffer, size.Width, size.Height, clear, checkerboard, cellSize);
            }
            finally
            {
                gl.BindFramebuffer(BrowserGlesInterface.DrawFramebuffer, (uint)drawFramebuffer);
                gl.BindFramebuffer(BrowserGlesInterface.ReadFramebuffer, (uint)readFramebuffer);
                // Disposing the platform lease resets Skia's cached GL state.
            }
        }

        // Re-wrap each frame: SKImage is immutable, but our texture is reused.
        // BottomLeft handles GL orientation without a CPU-side row flip.
        using GRBackendTexture backend = new(size.Width, size.Height, false,
            new GRGlTextureInfo(BrowserGlesInterface.Texture2D, _texture, BrowserGlesInterface.Rgba8));
        using SKImage image = SKImage.FromTexture(grContext, backend, GRSurfaceOrigin.BottomLeft,
            SKColorType.Rgba8888, SKAlphaType.Premul)
            ?? throw new InvalidOperationException("Could not wrap the WebGL2 output texture for Skia.");
        using SKPaint paint = new() { Color = SKColors.White.WithAlpha((byte)Math.Round(lease.CurrentOpacity * 255)) };
        lease.SkCanvas.DrawImage(image, new SKRect(0, 0, size.Width, size.Height),
            new SKRect((float)bounds.X, (float)bounds.Y, (float)bounds.Right, (float)bounds.Bottom),
            new SKSamplingOptions(SKFilterMode.Linear), paint);
        // Submit sampling before the next frame overwrites/deletes our texture. No CPU wait/readback.
        grContext.Flush();
    }

    private void EnsureOutput(PixelSize size)
    {
        if (_texture != 0 && _size == size)
            return;
        BrowserGlesInterface gl = _gl!;
        gl.GetIntegerv(0x0D33 /* MAX_TEXTURE_SIZE */, out int maxSize);
        if (size.Width > maxSize || size.Height > maxSize)
            throw new InvalidOperationException($"Preview size {size} exceeds WebGL2 texture limit {maxSize}.");
        DeleteOutput();
        uint[] textures = new uint[1];
        gl.GenTextures(1, textures);
        _texture = textures[0];
        try
        {
            gl.BindTexture(BrowserGlesInterface.Texture2D, _texture);
            gl.TexParameteri(BrowserGlesInterface.Texture2D, 0x2801, 0x2601); // MIN_FILTER, LINEAR
            gl.TexParameteri(BrowserGlesInterface.Texture2D, 0x2800, 0x2601); // MAG_FILTER, LINEAR
            gl.TexParameteri(BrowserGlesInterface.Texture2D, 0x2802, 0x812F); // WRAP_S, CLAMP_TO_EDGE
            gl.TexParameteri(BrowserGlesInterface.Texture2D, 0x2803, 0x812F); // WRAP_T, CLAMP_TO_EDGE
            gl.TexImage2D(BrowserGlesInterface.Texture2D, 0, (int)BrowserGlesInterface.Rgba8,
                size.Width, size.Height, 0, 0x1908 /* RGBA */, 0x1401 /* UNSIGNED_BYTE */, IntPtr.Zero);
            _framebuffer = gl.CreateFramebuffer(_texture);
            _size = size;
        }
        catch
        {
            DeleteOutput();
            throw;
        }
    }

    private void DeleteOutput()
    {
        if (_framebuffer != 0)
            _gl!.DeleteFramebuffer(_framebuffer);
        if (_texture != 0)
            _gl!.DeleteTextures(1, [_texture]);
        _framebuffer = _texture = 0;
        _size = default;
    }

    private void DisposeResources()
    {
        if (_context is not null && !_context.IsLost && _grContext is { IsAbandoned: false })
        {
            using (_context.EnsureCurrent())
            {
                _grContext.Flush();
                _renderer.Deinitialize();
                DeleteOutput();
                _grContext.ResetContext();
            }
        }
        // Never delete handles from a lost context in a replacement context.
        _renderer = new OpenGlRenderer();
        _framebuffer = _texture = 0;
        _size = default;
        _gl = null;
        _context = null;
        _grContext = null;
    }
}
