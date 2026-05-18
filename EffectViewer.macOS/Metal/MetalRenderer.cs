using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using EffectViewer.Rendering;
using EffectViewer.Rendering.TextureUpload;
using Foundation;
using Metal;

namespace EffectViewer.macOS.Metal
{
    internal sealed class MetalRenderer : IDisposable
    {
        private const int FloatsPerVertex = 8;
        private const int SpriteVertexCount = 6;
        private const int VertexBufferCount = 3;
        private static readonly RenderTextureRef WhiteTexture = new(FrameCaptureGraphics.WhiteTextureId);

        private readonly IMTLDevice _device;
        private readonly IMTLCommandQueue _commandQueue;
        private readonly IMTLSamplerState _sampler;
        private readonly Dictionary<RenderTextureRef, CachedTexture> _textures = [];
        private readonly IMTLBuffer?[] _vertexBuffers = new IMTLBuffer?[VertexBufferCount];
        private readonly nuint[] _vertexBufferLengths = new nuint[VertexBufferCount];
        private readonly RenderVertex[] _spriteVertices = new RenderVertex[SpriteVertexCount];
        private readonly List<RenderVertex> _transformedVertices = [];
        private float[] _packedVertices = [];
        private IMTLBuffer? _currentVertexBuffer;
        private nuint _currentVertexBufferLength;
        private nuint _vertexBufferWriteOffset;
        private int _vertexBufferIndex = -1;
        private IMTLRenderPipelineState? _normalPipeline;
        private IMTLRenderPipelineState? _additivePipeline;
        private IMTLRenderPipelineState? _checkerboardPipeline;
        private MTLPixelFormat _pipelinePixelFormat = MTLPixelFormat.Invalid;
        private ITextureSource _textureSource = new GeneratedTextureSource();
        private bool _disposed;

        public MetalRenderer(IMTLDevice device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _commandQueue = _device.CreateCommandQueue() ?? throw new InvalidOperationException("Unable to create Metal command queue.");
            _sampler = CreateSamplerState();
        }

        public ITextureSource TextureSource
        {
            get => _textureSource;
            set
            {
                if (ReferenceEquals(_textureSource, value))
                {
                    return;
                }

                _textureSource = value ?? new GeneratedTextureSource();
                DeleteCachedTextures();
            }
        }

        public float ViewZoom { get; set; } = 1f;
        public Vector2 ViewPan { get; set; } = Vector2.Zero;

        public bool Render(
            IMTLTexture targetTexture,
            int width,
            int height,
            RenderFrame frame,
            Vector4 clearColor,
            Vector4? checkerboardColor,
            int checkerboardCellSizePixels,
            Action<IMTLCommandBuffer>? beforeCommit = null)
        {
            if (_disposed || targetTexture is null || width <= 0 || height <= 0)
            {
                return false;
            }

            EnsurePipelines(targetTexture.PixelFormat);
            if (_normalPipeline is null || _additivePipeline is null || _checkerboardPipeline is null)
            {
                return false;
            }

            frame ??= new RenderFrame();
            BeginFrameVertexUpload(frame, width, height, checkerboardColor, checkerboardCellSizePixels);

            using MTLRenderPassDescriptor passDescriptor = MTLRenderPassDescriptor.CreateRenderPassDescriptor();
            MTLRenderPassColorAttachmentDescriptor colorAttachment = passDescriptor.ColorAttachments[0];
            colorAttachment.Texture = targetTexture;
            colorAttachment.LoadAction = MTLLoadAction.Clear;
            colorAttachment.StoreAction = MTLStoreAction.Store;
            colorAttachment.ClearColor = new MTLClearColor(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W);

            IMTLCommandBuffer? commandBuffer = _commandQueue.CommandBuffer();
            if (commandBuffer is null)
            {
                return false;
            }

            IMTLRenderCommandEncoder? encoder = commandBuffer.CreateRenderCommandEncoder(passDescriptor);
            if (encoder is null)
            {
                commandBuffer.Commit();
                return false;
            }

            if (checkerboardColor.HasValue)
            {
                DrawCheckerboard(encoder, width, height, checkerboardColor.Value, checkerboardCellSizePixels);
            }

            DrawSprites(encoder, frame.Sprites, width, height);
            DrawMeshes(encoder, frame, width, height);

            encoder.EndEncoding();
            beforeCommit?.Invoke(commandBuffer);
            commandBuffer.Commit();
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DeleteCachedTextures();
            for (int i = 0; i < _vertexBuffers.Length; i++)
            {
                _vertexBuffers[i]?.Dispose();
                _vertexBuffers[i] = null;
                _vertexBufferLengths[i] = 0;
            }

            _normalPipeline?.Dispose();
            _additivePipeline?.Dispose();
            _checkerboardPipeline?.Dispose();
            _sampler.Dispose();
            _commandQueue.Dispose();
        }

        private IMTLSamplerState CreateSamplerState()
        {
            using MTLSamplerDescriptor descriptor = new()
            {
                MinFilter = MTLSamplerMinMagFilter.Linear,
                MagFilter = MTLSamplerMinMagFilter.Linear,
                SAddressMode = MTLSamplerAddressMode.ClampToEdge,
                TAddressMode = MTLSamplerAddressMode.ClampToEdge
            };

            return _device.CreateSamplerState(descriptor) ?? throw new InvalidOperationException("Unable to create Metal sampler state.");
        }

        private void EnsurePipelines(MTLPixelFormat pixelFormat)
        {
            if (_normalPipeline is not null &&
                _additivePipeline is not null &&
                _pipelinePixelFormat == pixelFormat)
            {
                return;
            }

            _normalPipeline?.Dispose();
            _additivePipeline?.Dispose();
            _checkerboardPipeline?.Dispose();
            _normalPipeline = null;
            _additivePipeline = null;
            _checkerboardPipeline = null;

            using IMTLLibrary? library = CreateShaderLibrary();
            if (library is null)
            {
                return;
            }

            using IMTLFunction? vertexFunction = library.CreateFunction("vertex_main");
            using IMTLFunction? fragmentFunction = library.CreateFunction("fragment_main");
            using IMTLFunction? checkerboardFragmentFunction = library.CreateFunction("fragment_checkerboard");
            if (vertexFunction is null || fragmentFunction is null || checkerboardFragmentFunction is null)
            {
                return;
            }

            _normalPipeline = CreatePipeline(pixelFormat, vertexFunction, fragmentFunction, RenderBlendMode.Normal);
            _additivePipeline = CreatePipeline(pixelFormat, vertexFunction, fragmentFunction, RenderBlendMode.Additive);
            _checkerboardPipeline = CreatePipeline(pixelFormat, vertexFunction, checkerboardFragmentFunction, RenderBlendMode.Normal);
            _pipelinePixelFormat = pixelFormat;
        }

        private IMTLLibrary? CreateShaderLibrary()
        {
            NSError? error;
            using MTLCompileOptions options = new();
            IMTLLibrary? library = _device.CreateLibrary(ShaderSource, options, out error);
            if (library is null)
            {
                throw new InvalidOperationException($"Unable to compile Metal shaders: {error?.LocalizedDescription ?? "unknown error"}");
            }

            return library;
        }

        private IMTLRenderPipelineState CreatePipeline(
            MTLPixelFormat pixelFormat,
            IMTLFunction vertexFunction,
            IMTLFunction fragmentFunction,
            RenderBlendMode blendMode)
        {
            using MTLRenderPipelineDescriptor descriptor = new()
            {
                VertexFunction = vertexFunction,
                FragmentFunction = fragmentFunction
            };

            MTLRenderPipelineColorAttachmentDescriptor colorAttachment = descriptor.ColorAttachments[0];
            colorAttachment.PixelFormat = pixelFormat;
            colorAttachment.BlendingEnabled = true;
            colorAttachment.RgbBlendOperation = MTLBlendOperation.Add;
            colorAttachment.AlphaBlendOperation = MTLBlendOperation.Add;
            colorAttachment.SourceRgbBlendFactor = MTLBlendFactor.One;
            colorAttachment.SourceAlphaBlendFactor = MTLBlendFactor.One;
            colorAttachment.DestinationAlphaBlendFactor = MTLBlendFactor.OneMinusSourceAlpha;
            colorAttachment.DestinationRgbBlendFactor = blendMode == RenderBlendMode.Additive
                ? MTLBlendFactor.One
                : MTLBlendFactor.OneMinusSourceAlpha;

            NSError? error;
            IMTLRenderPipelineState? pipeline = _device.CreateRenderPipelineState(descriptor, out error);
            if (pipeline is null)
            {
                throw new InvalidOperationException($"Unable to create Metal pipeline: {error?.LocalizedDescription ?? "unknown error"}");
            }

            return pipeline;
        }

        private void DrawSprites(IMTLRenderCommandEncoder encoder, List<RenderSpriteCommand> sprites, int width, int height)
        {
            if (sprites.Count == 0)
            {
                return;
            }

            ReadOnlySpan<RenderSpriteCommand> spriteSpan = CollectionsMarshal.AsSpan(sprites);
            foreach (ref readonly RenderSpriteCommand sprite in spriteSpan)
            {
                if (!TryGetTexture(sprite.Texture, out IMTLTexture? texture))
                {
                    continue;
                }

                FillSpriteVertices(_spriteVertices, sprite, width, height, ViewZoom, ViewPan);
                DrawTexturedTriangles(encoder, _spriteVertices, texture, sprite.BlendMode);
            }
        }

        private void DrawMeshes(IMTLRenderCommandEncoder encoder, RenderFrame frame, int width, int height)
        {
            List<RenderMeshCommand> meshes = frame.Meshes;
            if (meshes.Count == 0)
            {
                return;
            }

            ReadOnlySpan<RenderMeshCommand> meshSpan = CollectionsMarshal.AsSpan(meshes);
            foreach (ref readonly RenderMeshCommand mesh in meshSpan)
            {
                if (mesh.VertexCount == 0 || !TryGetTexture(mesh.Texture, out IMTLTexture? texture))
                {
                    continue;
                }

                ReadOnlySpan<RenderVertex> sourceVertices = frame.GetMeshVertices(mesh);
                _transformedVertices.Clear();
                _transformedVertices.EnsureCapacity(sourceVertices.Length);
                CollectionsMarshal.SetCount(_transformedVertices, sourceVertices.Length);
                Span<RenderVertex> transformedVertices = CollectionsMarshal.AsSpan(_transformedVertices);
                for (int i = 0; i < sourceVertices.Length; i++)
                {
                    RenderVertex vertex = sourceVertices[i];
                    transformedVertices[i] = new RenderVertex(
                        new Vector2(
                            ToClipX(ApplyViewX(vertex.Position.X, ViewZoom, ViewPan), width),
                            ToClipY(ApplyViewY(vertex.Position.Y, ViewZoom, ViewPan), height)),
                        vertex.Uv,
                        vertex.Color);
                }

                DrawTexturedTriangles(encoder, transformedVertices, texture, mesh.BlendMode);
            }
        }

        private void DrawTexturedTriangles(
            IMTLRenderCommandEncoder encoder,
            ReadOnlySpan<RenderVertex> vertices,
            IMTLTexture texture,
            RenderBlendMode blendMode)
        {
            if (vertices.Length == 0)
            {
                return;
            }

            VertexUpload vertexUpload = UploadVertices(vertices);
            IMTLRenderPipelineState? pipeline = blendMode == RenderBlendMode.Additive ? _additivePipeline : _normalPipeline;
            if (pipeline is null)
            {
                return;
            }

            encoder.SetRenderPipelineState(pipeline);
            encoder.SetVertexBuffer(vertexUpload.Buffer, vertexUpload.Offset, 0);
            encoder.SetFragmentTexture(texture, 0);
            encoder.SetFragmentSamplerState(_sampler, 0);
            encoder.DrawPrimitives(MTLPrimitiveType.Triangle, 0, (nuint)vertices.Length);
        }

        private VertexUpload UploadVertices(ReadOnlySpan<RenderVertex> vertices)
        {
            EnsurePackedVertexCapacity(vertices.Length * FloatsPerVertex);
            int offset = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                ref readonly RenderVertex vertex = ref vertices[i];
                AppendVertex(
                    _packedVertices,
                    ref offset,
                    vertex.Position.X,
                    vertex.Position.Y,
                    vertex.Uv.X,
                    vertex.Uv.Y,
                    vertex.Color);
            }

            return UploadPackedVertices(_packedVertices, offset);
        }

        private unsafe VertexUpload UploadPackedVertices(float[] vertices, int floatCount)
        {
            nuint byteCount = checked((nuint)(floatCount * sizeof(float)));
            if (_currentVertexBuffer is null || _vertexBufferWriteOffset + byteCount > _currentVertexBufferLength)
            {
                throw new InvalidOperationException("Metal vertex buffer is too small for the current frame.");
            }

            fixed (float* source = vertices)
            {
                byte* destination = (byte*)_currentVertexBuffer.Contents + _vertexBufferWriteOffset;
                Buffer.MemoryCopy(source, destination, (long)(_currentVertexBufferLength - _vertexBufferWriteOffset), (long)byteCount);
            }

            nuint vertexOffset = _vertexBufferWriteOffset;
            _currentVertexBuffer.DidModify(new NSRange((nint)vertexOffset, (nint)byteCount));
            _vertexBufferWriteOffset += byteCount;
            return new VertexUpload(_currentVertexBuffer, vertexOffset);
        }

        private void BeginFrameVertexUpload(
            RenderFrame frame,
            int width,
            int height,
            Vector4? checkerboardColor,
            int checkerboardCellSizePixels)
        {
            int vertexCount = checked(frame.Sprites.Count * SpriteVertexCount + GetMeshVertexCount(frame));
            if (checkerboardColor.HasValue)
            {
                vertexCount = checked(vertexCount + SpriteVertexCount);
            }

            nuint byteCount = checked((nuint)Math.Max(vertexCount, 1) * FloatsPerVertex * sizeof(float));
            _vertexBufferIndex = (_vertexBufferIndex + 1) % VertexBufferCount;
            EnsureVertexBuffer(_vertexBufferIndex, byteCount);
            _currentVertexBuffer = _vertexBuffers[_vertexBufferIndex];
            _currentVertexBufferLength = _vertexBufferLengths[_vertexBufferIndex];
            _vertexBufferWriteOffset = 0;
        }

        private static int GetMeshVertexCount(RenderFrame frame)
        {
            int vertexCount = 0;
            ReadOnlySpan<RenderMeshCommand> meshes = CollectionsMarshal.AsSpan(frame.Meshes);
            foreach (ref readonly RenderMeshCommand mesh in meshes)
            {
                vertexCount = checked(vertexCount + mesh.VertexCount);
            }

            return vertexCount;
        }

        private void EnsureVertexBuffer(int index, nuint byteCount)
        {
            if (_vertexBuffers[index] is not null && _vertexBufferLengths[index] >= byteCount)
            {
                return;
            }

            _vertexBuffers[index]?.Dispose();
            nuint capacity = _vertexBufferLengths[index] == 0 ? (nuint)(FloatsPerVertex * 64 * sizeof(float)) : _vertexBufferLengths[index];
            while (capacity < byteCount)
            {
                capacity *= 2;
            }

            _vertexBuffers[index] = _device.CreateBuffer(capacity, MTLResourceOptions.StorageModeShared);
            _vertexBufferLengths[index] = capacity;
            if (_vertexBuffers[index] is null)
            {
                throw new InvalidOperationException("Unable to create Metal vertex buffer.");
            }
        }

        private void DrawCheckerboard(IMTLRenderCommandEncoder encoder, int width, int height, Vector4 alternateColor, int cellSizePixels)
        {
            int cellSize = Math.Max(4, cellSizePixels);
            EnsurePackedVertexCapacity(SpriteVertexCount * FloatsPerVertex);
            float[] vertices = _packedVertices;
            int offset = 0;
            float clipLeft = ToClipX(0, width);
            float clipRight = ToClipX(width, width);
            float clipTop = ToClipY(0, height);
            float clipBottom = ToClipY(height, height);
            float cellScale = 1f / cellSize;

            AppendVertex(vertices, ref offset, clipLeft, clipTop, cellScale, cellScale, alternateColor);
            AppendVertex(vertices, ref offset, clipRight, clipTop, cellScale, cellScale, alternateColor);
            AppendVertex(vertices, ref offset, clipRight, clipBottom, cellScale, cellScale, alternateColor);
            AppendVertex(vertices, ref offset, clipLeft, clipTop, cellScale, cellScale, alternateColor);
            AppendVertex(vertices, ref offset, clipRight, clipBottom, cellScale, cellScale, alternateColor);
            AppendVertex(vertices, ref offset, clipLeft, clipBottom, cellScale, cellScale, alternateColor);

            if (offset == 0 || _checkerboardPipeline is null)
            {
                return;
            }

            VertexUpload vertexUpload = UploadPackedVertices(vertices, offset);
            encoder.SetRenderPipelineState(_checkerboardPipeline);
            encoder.SetVertexBuffer(vertexUpload.Buffer, vertexUpload.Offset, 0);
            encoder.DrawPrimitives(MTLPrimitiveType.Triangle, 0, (nuint)(offset / FloatsPerVertex));
        }

        private bool TryGetTexture(RenderTextureRef texture, [NotNullWhen(true)] out IMTLTexture? metalTexture)
        {
            int revision = GetTextureRevision(texture);
            if (_textures.TryGetValue(texture, out CachedTexture cached) && cached.Revision == revision)
            {
                metalTexture = cached.Texture;
                return true;
            }

            DeleteCachedTexture(texture);
            metalTexture = CreateTexture(texture, out int loadedRevision);
            if (metalTexture is null)
            {
                return false;
            }

            _textures[texture] = new CachedTexture(metalTexture, loadedRevision);
            return true;
        }

        private IMTLTexture? CreateTexture(RenderTextureRef texture, out int revision)
        {
            revision = 0;
            if (!_textureSource.TryLoad(texture, out TextureUploadData? data) ||
                data.Width <= 0 ||
                data.Height <= 0 ||
                data.RgbaPixels is null ||
                !TryGetRgbaByteCount(data.Width, data.Height, out int byteCount) ||
                data.RgbaPixels.Length < byteCount)
            {
                return null;
            }

            revision = data.Revision;
            using MTLTextureDescriptor descriptor = MTLTextureDescriptor.CreateTexture2DDescriptor(
                MTLPixelFormat.RGBA8Unorm,
                (nuint)data.Width,
                (nuint)data.Height,
                false);
            descriptor.Usage = MTLTextureUsage.ShaderRead;
            descriptor.StorageMode = MTLStorageMode.Shared;

            IMTLTexture? metalTexture = _device.CreateTexture(descriptor);
            if (metalTexture is null)
            {
                return null;
            }

            byte[] pixels = CreatePremultipliedPixels(data.RgbaPixels, byteCount);
            UploadTexturePixels(metalTexture, pixels, data.Width, data.Height);
            return metalTexture;
        }

        private unsafe static void UploadTexturePixels(IMTLTexture texture, byte[] pixels, int width, int height)
        {
            fixed (byte* pixelsPtr = pixels)
            {
                texture.ReplaceRegion(
                    new MTLRegion(
                        new MTLOrigin(0, 0, 0),
                        new MTLSize(width, height, 1)),
                    0,
                    new IntPtr(pixelsPtr),
                    (nuint)(width * 4));
            }
        }

        private int GetTextureRevision(RenderTextureRef texture)
        {
            return _textureSource is ITextureRevisionSource revisionSource
                ? revisionSource.GetTextureRevision(texture)
                : 0;
        }

        private void DeleteCachedTexture(RenderTextureRef texture)
        {
            if (_textures.Remove(texture, out CachedTexture cached))
            {
                cached.Texture.Dispose();
            }
        }

        private void DeleteCachedTextures()
        {
            foreach (CachedTexture cached in _textures.Values)
            {
                cached.Texture.Dispose();
            }

            _textures.Clear();
        }

        private static byte[] CreatePremultipliedPixels(byte[] source, int byteCount)
        {
            byte[] pixels = new byte[byteCount];
            Array.Copy(source, pixels, byteCount);

            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte alpha = pixels[i + 3];
                if (alpha == byte.MaxValue)
                {
                    continue;
                }

                pixels[i + 0] = Premultiply(pixels[i + 0], alpha);
                pixels[i + 1] = Premultiply(pixels[i + 1], alpha);
                pixels[i + 2] = Premultiply(pixels[i + 2], alpha);
            }

            return pixels;
        }

        private static byte Premultiply(byte color, byte alpha)
        {
            return (byte)((color * alpha + 127) / 255);
        }

        private static bool TryGetRgbaByteCount(int width, int height, out int byteCount)
        {
            try
            {
                byteCount = checked(width * height * 4);
                return true;
            }
            catch (OverflowException)
            {
                byteCount = 0;
                return false;
            }
        }

        private static void FillSpriteVertices(
            Span<RenderVertex> vertices,
            RenderSpriteCommand sprite,
            int width,
            int height,
            float zoom,
            Vector2 pan)
        {
            float pixelX = sprite.Position.X <= 1f ? sprite.Position.X * width : sprite.Position.X;
            float pixelY = sprite.Position.Y <= 1f ? sprite.Position.Y * height : sprite.Position.Y;
            float pixelW = sprite.Size.X <= 1f ? sprite.Size.X * width : sprite.Size.X;
            float pixelH = sprite.Size.Y <= 1f ? sprite.Size.Y * height : sprite.Size.Y;

            float left = ToClipX(ApplyViewX(pixelX - pixelW / 2f, zoom, pan), width);
            float right = ToClipX(ApplyViewX(pixelX + pixelW / 2f, zoom, pan), width);
            float top = ToClipY(ApplyViewY(pixelY - pixelH / 2f, zoom, pan), height);
            float bottom = ToClipY(ApplyViewY(pixelY + pixelH / 2f, zoom, pan), height);

            Vector4 uv = sprite.UvRect;
            Vector4 color = sprite.Color;

            vertices[0] = new RenderVertex(new Vector2(left, top), new Vector2(uv.X, uv.Y), color);
            vertices[1] = new RenderVertex(new Vector2(right, top), new Vector2(uv.Z, uv.Y), color);
            vertices[2] = new RenderVertex(new Vector2(right, bottom), new Vector2(uv.Z, uv.W), color);
            vertices[3] = new RenderVertex(new Vector2(left, top), new Vector2(uv.X, uv.Y), color);
            vertices[4] = new RenderVertex(new Vector2(right, bottom), new Vector2(uv.Z, uv.W), color);
            vertices[5] = new RenderVertex(new Vector2(left, bottom), new Vector2(uv.X, uv.W), color);
        }

        private void EnsurePackedVertexCapacity(int floatCount)
        {
            if (_packedVertices.Length >= floatCount)
            {
                return;
            }

            int capacity = _packedVertices.Length == 0 ? FloatsPerVertex * 64 : _packedVertices.Length;
            while (capacity < floatCount)
            {
                capacity *= 2;
            }

            Array.Resize(ref _packedVertices, capacity);
        }

        private static void AppendVertex(float[] vertices, ref int offset, float x, float y, float u, float v, Vector4 color)
        {
            vertices[offset++] = x;
            vertices[offset++] = y;
            vertices[offset++] = u;
            vertices[offset++] = v;
            vertices[offset++] = color.X;
            vertices[offset++] = color.Y;
            vertices[offset++] = color.Z;
            vertices[offset++] = color.W;
        }

        private static void AppendScreenRect(
            float[] vertices,
            ref int offset,
            float left,
            float top,
            float right,
            float bottom,
            int width,
            int height,
            Vector4 color)
        {
            float clipLeft = ToClipX(left, width);
            float clipRight = ToClipX(right, width);
            float clipTop = ToClipY(top, height);
            float clipBottom = ToClipY(bottom, height);

            AppendVertex(vertices, ref offset, clipLeft, clipTop, 0f, 0f, color);
            AppendVertex(vertices, ref offset, clipRight, clipTop, 1f, 0f, color);
            AppendVertex(vertices, ref offset, clipRight, clipBottom, 1f, 1f, color);
            AppendVertex(vertices, ref offset, clipLeft, clipTop, 0f, 0f, color);
            AppendVertex(vertices, ref offset, clipRight, clipBottom, 1f, 1f, color);
            AppendVertex(vertices, ref offset, clipLeft, clipBottom, 0f, 1f, color);
        }

        private static float ToClipX(float x, int width)
        {
            return width <= 0 ? 0f : x / width * 2f - 1f;
        }

        private static float ToClipY(float y, int height)
        {
            return height <= 0 ? 0f : 1f - y / height * 2f;
        }

        private static float ApplyViewX(float x, float zoom, Vector2 pan)
        {
            return x * zoom + pan.X;
        }

        private static float ApplyViewY(float y, float zoom, Vector2 pan)
        {
            return y * zoom + pan.Y;
        }

        private readonly record struct CachedTexture(IMTLTexture Texture, int Revision);
        private readonly record struct VertexUpload(IMTLBuffer Buffer, nuint Offset);

        private const string ShaderSource = """
            #include <metal_stdlib>
            using namespace metal;

            struct VertexOut
            {
                float4 position [[position]];
                float2 uv;
                float4 color;
            };

            vertex VertexOut vertex_main(uint vertexId [[vertex_id]], const device float* vertices [[buffer(0)]])
            {
                uint offset = vertexId * 8;
                VertexOut outVertex;
                outVertex.position = float4(vertices[offset + 0], vertices[offset + 1], 0.0, 1.0);
                outVertex.uv = float2(vertices[offset + 2], vertices[offset + 3]);
                outVertex.color = float4(vertices[offset + 4], vertices[offset + 5], vertices[offset + 6], vertices[offset + 7]);
                return outVertex;
            }

            fragment float4 fragment_main(VertexOut in [[stage_in]], texture2d<float> tex [[texture(0)]], sampler textureSampler [[sampler(0)]])
            {
                float4 color = tex.sample(textureSampler, in.uv) * in.color;
                color.rgb *= in.color.a;
                return color;
            }

            fragment float4 fragment_checkerboard(VertexOut in [[stage_in]])
            {
                uint cellX = uint(floor(in.position.x * in.uv.x));
                uint cellY = uint(floor(in.position.y * in.uv.y));
                if (((cellX + cellY) & 1u) == 0u)
                {
                    return float4(0.0);
                }

                return in.color;
            }
            """;
    }
}
