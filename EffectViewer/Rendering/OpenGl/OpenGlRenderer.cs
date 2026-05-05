using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using EffectViewer.Rendering.Gl;
using EffectViewer.Rendering.TextureUpload;

namespace EffectViewer.Rendering.OpenGl
{
    public sealed class OpenGlRenderer
    {
        private const int FloatsPerVertex = 8;
        private static readonly RenderTextureRef WhiteTexture = new(FrameCaptureGraphics.WhiteTextureId);

        private enum ShaderDialect
        {
            Legacy,
            Core
        }

        private uint _program;
        private uint _vertexBuffer;
        private uint _vertexArray;
        private int _positionLocation = -1;
        private int _uvLocation = -1;
        private int _colorLocation = -1;
        private int _textureLocation = -1;
        private bool _canDrawSprites;
        private readonly OpenGlTextureCache _textureCache = new();
        private ITextureSource _textureSource = new GeneratedTextureSource();
        private int _maxTextureSize;

        private IEffectGlInterface _gl;

        public string BackendName => _gl?.BackendName ?? "OpenGL";
        public bool IsInitialized { get; private set; }
        public float ViewZoom { get; set; } = 1f;
        public Vector2 ViewPan { get; set; } = Vector2.Zero;

        public ITextureSource TextureSource
        {
            get => _textureSource;
            set
            {
                _textureSource = value ?? new GeneratedTextureSource();
                _textureCache.Clear();
            }
        }

        public void Initialize(IEffectGlInterface gl)
        {
            ArgumentNullException.ThrowIfNull(gl);

            _gl = gl;

            if (!gl.CanDraw)
            {
                IsInitialized = gl.CanClear;
                return;
            }

            _program = CreateProgram();
            if (gl.SupportsVertexArrayObjects)
            {
                uint[] arrays = new uint[1];
                _gl.GenVertexArrays(1, arrays);
                _vertexArray = arrays[0];
                _gl.BindVertexArray(_vertexArray);
            }

            uint[] buffers = new uint[1];
            _gl.GenBuffers(1, buffers);
            _vertexBuffer = buffers[0];

            _positionLocation = _gl.GetAttribLocation(_program, "a_position");
            _uvLocation = _gl.GetAttribLocation(_program, "a_uv");
            _colorLocation = _gl.GetAttribLocation(_program, "a_color");
            _textureLocation = _gl.GetUniformLocation(_program, "u_texture");

            bool hasRequiredVertexArray = !gl.SupportsVertexArrayObjects || _vertexArray != 0;
            _canDrawSprites = _program != 0 &&
                              _vertexBuffer != 0 &&
                              hasRequiredVertexArray &&
                              _positionLocation >= 0 &&
                              _uvLocation >= 0 &&
                              _colorLocation >= 0 &&
                              _textureLocation >= 0;
            IsInitialized = true;
        }

        public void Render(
            RenderFrame frame,
            int framebuffer,
            int width,
            int height,
            Vector4? clearColorOverride = null,
            Vector4? checkerboardColor = null,
            int checkerboardCellSizePixels = 16)
        {
            if (!IsInitialized)
            {
                return;
            }

            _gl.BindFramebuffer(OpenGlConstants.Framebuffer, (uint)framebuffer);

            _gl.Viewport(0, 0, Math.Max(1, width), Math.Max(1, height));
            Vector4 clear = clearColorOverride ?? frame.ClearColor;
            _gl.ClearColor(clear.X, clear.Y, clear.Z, clear.W);
            _gl.Enable(OpenGlConstants.Blend);
            SetBlendMode(RenderBlendMode.Normal);

            _gl.Clear(OpenGlConstants.ColorBufferBit);
            if (checkerboardColor.HasValue)
            {
                DrawCheckerboard(width, height, checkerboardColor.Value, checkerboardCellSizePixels);
            }

            DrawSprites(frame.Sprites, width, height);
            DrawMeshes(frame.Meshes, width, height);
        }

        public void Deinitialize()
        {
            DeleteCachedTextures();

            if (_gl != null && _vertexBuffer != 0)
            {
                _gl.DeleteBuffers(1, [_vertexBuffer]);
            }

            if (_gl != null && _vertexArray != 0)
            {
                _gl.DeleteVertexArrays(1, [_vertexArray]);
            }

            if (_gl != null && _program != 0)
            {
                _gl.DeleteProgram(_program);
            }

            _program = 0;
            _vertexBuffer = 0;
            _vertexArray = 0;
            _positionLocation = -1;
            _uvLocation = -1;
            _colorLocation = -1;
            _textureLocation = -1;
            _canDrawSprites = false;
            _maxTextureSize = 0;
            _textureCache.Clear();

            _gl = null;
            IsInitialized = false;
        }

        private void DeleteCachedTextures()
        {
            if (_gl == null)
            {
                _textureCache.Clear();
                return;
            }

            foreach (int handle in _textureCache.Handles)
            {
                if (handle != 0)
                {
                    _gl.DeleteTextures(1, [(uint)handle]);
                }
            }

            _textureCache.Clear();
        }

        private void DrawSprites(IReadOnlyCollection<RenderSpriteCommand> sprites, int width, int height)
        {
            if (!_canDrawSprites || sprites.Count == 0 || _program == 0 || _vertexBuffer == 0)
            {
                return;
            }

            foreach (RenderSpriteCommand sprite in sprites)
            {
                if (!TryGetTextureSet(sprite.Texture, out OpenGlTextureSet textureSet))
                {
                    continue;
                }

                SetBlendMode(sprite.BlendMode);
                DrawTexturedTriangles(
                    CreateSpriteVertices(sprite, width, height, ViewZoom, ViewPan),
                    textureSet);
            }
        }

        private void DrawMeshes(IReadOnlyCollection<RenderMeshCommand> meshes, int width, int height)
        {
            if (!_canDrawSprites || meshes.Count == 0 || _program == 0 || _vertexBuffer == 0)
            {
                return;
            }

            foreach (RenderMeshCommand mesh in meshes)
            {
                if (mesh.Vertices.Count == 0)
                {
                    continue;
                }

                if (!TryGetTextureSet(mesh.Texture, out OpenGlTextureSet textureSet))
                {
                    continue;
                }

                List<RenderVertex> vertices = new(mesh.Vertices.Count);
                foreach (RenderVertex vertex in mesh.Vertices)
                {
                    vertices.Add(new RenderVertex(
                        new Vector2(
                            ToClipX(ApplyViewX(vertex.Position.X, ViewZoom, ViewPan), width),
                            ToClipY(ApplyViewY(vertex.Position.Y, ViewZoom, ViewPan), height)),
                        vertex.Uv,
                        vertex.Color));
                }

                SetBlendMode(mesh.BlendMode);
                DrawTexturedTriangles(vertices, textureSet);
            }
        }

        private void DrawTexturedTriangles(IReadOnlyList<RenderVertex> vertices, OpenGlTextureSet textureSet)
        {
            if (vertices.Count == 0 || textureSet.Handles.Count == 0)
            {
                return;
            }

            if (!textureSet.Layout.IsTiled)
            {
                UploadVertices(vertices);
                if (BindTextureHandle(textureSet.Handles[0]))
                {
                    _gl.DrawArrays(OpenGlConstants.Triangles, 0, vertices.Count);
                }

                return;
            }

            foreach (TextureTileDrawBatch batch in TextureTileClipper.CreateBatches(textureSet.Layout, vertices))
            {
                if (batch.Vertices.Count == 0)
                {
                    continue;
                }

                UploadVertices(batch.Vertices);
                if (BindTextureHandle(textureSet.GetHandle(batch.Tile)))
                {
                    _gl.DrawArrays(OpenGlConstants.Triangles, 0, batch.Vertices.Count);
                }
            }
        }

        private void DrawCheckerboard(int width, int height, Vector4 alternateColor, int cellSizePixels)
        {
            if (!_canDrawSprites || _program == 0 || _vertexBuffer == 0)
            {
                return;
            }

            int cellSize = Math.Max(4, cellSizePixels);
            int columns = Math.Max(1, (width + cellSize - 1) / cellSize);
            int rows = Math.Max(1, (height + cellSize - 1) / cellSize);
            int squareCount = (columns * rows + 1) / 2;
            float[] vertices = new float[squareCount * 6 * FloatsPerVertex];
            int offset = 0;

            for (int row = 0; row < rows; row++)
            {
                int top = row * cellSize;
                int bottom = Math.Min(height, top + cellSize);
                for (int column = 0; column < columns; column++)
                {
                    if (((row + column) & 1) == 0)
                    {
                        continue;
                    }

                    int left = column * cellSize;
                    int right = Math.Min(width, left + cellSize);
                    AppendScreenRect(vertices, ref offset, left, top, right, bottom, width, height, alternateColor);
                }
            }

            if (offset == 0 || !BindTexture(WhiteTexture))
            {
                return;
            }

            if (offset != vertices.Length)
            {
                Array.Resize(ref vertices, offset);
            }

            UploadVertices(vertices);
            SetBlendMode(RenderBlendMode.Normal);
            _gl.DrawArrays(OpenGlConstants.Triangles, 0, offset / FloatsPerVertex);
        }

        private void SetBlendMode(RenderBlendMode blendMode)
        {
            if (blendMode == RenderBlendMode.Additive)
            {
                _gl.BlendFuncSeparate(
                    OpenGlConstants.One,
                    OpenGlConstants.One,
                    OpenGlConstants.One,
                    OpenGlConstants.OneMinusSrcAlpha);
            }
            else
            {
                _gl.BlendFunc(OpenGlConstants.One, OpenGlConstants.OneMinusSrcAlpha);
            }
        }

        private void UploadVertices(float[] vertices)
        {
            _gl.UseProgram(_program);
            _gl.Uniform1i(_textureLocation, 0);
            BindVertexArray();
            _gl.BindBuffer(OpenGlConstants.ArrayBuffer, _vertexBuffer);

            GCHandle verticesHandle = GCHandle.Alloc(vertices, GCHandleType.Pinned);
            try
            {
                _gl.BufferData(
                    OpenGlConstants.ArrayBuffer,
                    new IntPtr(vertices.Length * sizeof(float)),
                    verticesHandle.AddrOfPinnedObject(),
                    OpenGlConstants.StreamDraw);
            }
            finally
            {
                verticesHandle.Free();
            }

            int stride = FloatsPerVertex * sizeof(float);
            _gl.EnableVertexAttribArray((uint)_positionLocation);
            _gl.VertexAttribPointer((uint)_positionLocation, 2, OpenGlConstants.Float, false, stride, IntPtr.Zero);

            if (_uvLocation >= 0)
            {
                _gl.EnableVertexAttribArray((uint)_uvLocation);
                _gl.VertexAttribPointer((uint)_uvLocation, 2, OpenGlConstants.Float, false, stride, new IntPtr(2 * sizeof(float)));
            }

            _gl.EnableVertexAttribArray((uint)_colorLocation);
            _gl.VertexAttribPointer((uint)_colorLocation, 4, OpenGlConstants.Float, false, stride, new IntPtr(4 * sizeof(float)));
        }

        private void UploadVertices(IReadOnlyList<RenderVertex> vertices)
        {
            float[] packed = new float[vertices.Count * FloatsPerVertex];
            int offset = 0;
            foreach (RenderVertex vertex in vertices)
            {
                AppendVertex(
                    packed,
                    ref offset,
                    vertex.Position.X,
                    vertex.Position.Y,
                    vertex.Uv.X,
                    vertex.Uv.Y,
                    vertex.Color);
            }

            UploadVertices(packed);
        }

        private void BindVertexArray()
        {
            if (_vertexArray != 0)
            {
                _gl.BindVertexArray(_vertexArray);
            }
        }

        private bool BindTexture(RenderTextureRef texture)
        {
            return TryGetTextureSet(texture, out OpenGlTextureSet textureSet) &&
                   textureSet.Handles.Count > 0 &&
                   BindTextureHandle(textureSet.Handles[0]);
        }

        private bool BindTextureHandle(int handle)
        {
            if (handle == 0)
            {
                return false;
            }

            _gl.ActiveTexture(OpenGlConstants.Texture0);
            _gl.BindTexture(OpenGlConstants.Texture2D, (uint)handle);
            return true;
        }

        private bool TryGetTextureSet(RenderTextureRef texture, out OpenGlTextureSet textureSet)
        {
            int revision = GetTextureRevision(texture);
            if (!_textureCache.TryGetTextureSet(texture, revision, out textureSet))
            {
                DeleteCachedTexture(texture);
                textureSet = CreateTextureSet(texture, out int loadedRevision);
                if (textureSet is null)
                {
                    return false;
                }

                _textureCache.SetTextureSet(texture, textureSet, loadedRevision);
            }

            return true;
        }

        private OpenGlTextureSet CreateTextureSet(RenderTextureRef texture, out int revision)
        {
            revision = 0;
            int sourceByteCount;
            if (!_textureSource.TryLoad(texture, out TextureUploadData data) ||
                data.Width <= 0 ||
                data.Height <= 0 ||
                data.RgbaPixels is null ||
                !TryGetRgbaByteCount(data.Width, data.Height, out sourceByteCount) ||
                data.RgbaPixels.Length < sourceByteCount)
            {
                return null;
            }

            revision = data.Revision;
            TextureTileLayout layout = TextureTileLayout.Create(data.Width, data.Height, GetMaxTextureSize());
            List<int> handles = new(layout.Tiles.Count);
            foreach (TextureTile tile in layout.Tiles)
            {
                byte[] sourcePixels = layout.IsTiled
                    ? TextureTileLayout.CopyTilePixels(data, tile)
                    : data.RgbaPixels;
                int handle = CreateTexture(tile.UploadWidth, tile.UploadHeight, sourcePixels);
                if (handle == 0)
                {
                    DeleteTextureHandles(handles);
                    return null;
                }

                handles.Add(handle);
            }

            return new OpenGlTextureSet(layout, handles);
        }

        private int CreateTexture(int width, int height, byte[] rgbaPixels)
        {
            if (!TryGetRgbaByteCount(width, height, out int byteCount) ||
                rgbaPixels is null ||
                rgbaPixels.Length < byteCount)
            {
                return 0;
            }

            uint[] handles = new uint[1];
            _gl.GenTextures(1, handles);
            uint handle = handles[0];
            if (handle == 0)
            {
                return 0;
            }

            _gl.BindTexture(OpenGlConstants.Texture2D, handle);
            _gl.TexParameteri(OpenGlConstants.Texture2D, OpenGlConstants.TextureMinFilter, OpenGlConstants.Linear);
            _gl.TexParameteri(OpenGlConstants.Texture2D, OpenGlConstants.TextureMagFilter, OpenGlConstants.Linear);
            _gl.TexParameteri(OpenGlConstants.Texture2D, OpenGlConstants.TextureWrapS, OpenGlConstants.ClampToEdge);
            _gl.TexParameteri(OpenGlConstants.Texture2D, OpenGlConstants.TextureWrapT, OpenGlConstants.ClampToEdge);
            _gl.PixelStorei(OpenGlConstants.UnpackAlignment, 1);
            byte[] pixels = CreatePremultipliedPixels(rgbaPixels, byteCount);
            GCHandle pixelsHandle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                ClearTextureErrors();
                _gl.TexImage2D(
                    OpenGlConstants.Texture2D,
                    0,
                    (int)OpenGlConstants.Rgba,
                    width,
                    height,
                    0,
                    OpenGlConstants.Rgba,
                    OpenGlConstants.UnsignedByte,
                    pixelsHandle.AddrOfPinnedObject());
            }
            finally
            {
                pixelsHandle.Free();
            }

            if (_gl.GetError() != OpenGlConstants.NoError)
            {
                _gl.DeleteTextures(1, [handle]);
                return 0;
            }

            return (int)handle;
        }

        private int GetTextureRevision(RenderTextureRef texture)
        {
            return _textureSource is ITextureRevisionSource revisionSource
                ? revisionSource.GetTextureRevision(texture)
                : 0;
        }

        private int GetMaxTextureSize()
        {
            if (_maxTextureSize > 0)
            {
                return _maxTextureSize;
            }

            _gl.GetIntegerv(OpenGlConstants.MaxTextureSize, out int maxTextureSize);
            _maxTextureSize = maxTextureSize > 0 ? maxTextureSize : 4096;
            return _maxTextureSize;
        }

        private void DeleteCachedTexture(RenderTextureRef texture)
        {
            DeleteTextureHandles(_textureCache.RemoveTextureSet(texture));
        }

        private void DeleteTextureHandles(IEnumerable<int> handles)
        {
            foreach (int handle in handles)
            {
                if (handle != 0)
                {
                    _gl.DeleteTextures(1, [(uint)handle]);
                }
            }
        }

        private void ClearTextureErrors()
        {
            for (int i = 0; i < 8; i++)
            {
                if (_gl.GetError() == OpenGlConstants.NoError)
                {
                    return;
                }
            }
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

        private static RenderVertex[] CreateSpriteVertices(RenderSpriteCommand sprite, int width, int height, float zoom, Vector2 pan)
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

            return
            [
                new RenderVertex(new Vector2(left, top), new Vector2(uv.X, uv.Y), color),
                new RenderVertex(new Vector2(right, top), new Vector2(uv.Z, uv.Y), color),
                new RenderVertex(new Vector2(right, bottom), new Vector2(uv.Z, uv.W), color),
                new RenderVertex(new Vector2(left, top), new Vector2(uv.X, uv.Y), color),
                new RenderVertex(new Vector2(right, bottom), new Vector2(uv.Z, uv.W), color),
                new RenderVertex(new Vector2(left, bottom), new Vector2(uv.X, uv.W), color)
            ];
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

        private uint CreateProgram()
        {
            InvalidOperationException lastError = null;
            ShaderDialect[] dialects = _gl.Api == EffectGlApi.OpenGl
                ? [ShaderDialect.Legacy, ShaderDialect.Core]
                : [ShaderDialect.Legacy];

            foreach (ShaderDialect dialect in dialects)
            {
                try
                {
                    return CreateProgram(dialect);
                }
                catch (InvalidOperationException ex)
                {
                    lastError = ex;
                }
            }

            throw lastError ?? new InvalidOperationException($"Failed to create {BackendName} shader program.");
        }

        private uint CreateProgram(ShaderDialect dialect)
        {
            uint vertexShader = 0;
            uint fragmentShader = 0;
            uint program = 0;

            try
            {
                vertexShader = CompileShader(OpenGlConstants.VertexShader, GetVertexShaderSource(dialect));
                fragmentShader = CompileShader(OpenGlConstants.FragmentShader, GetFragmentShaderSource(dialect));

                program = _gl.CreateProgram();
                _gl.AttachShader(program, vertexShader);
                _gl.AttachShader(program, fragmentShader);
                _gl.LinkProgram(program);
                _gl.GetProgramiv(program, OpenGlConstants.LinkStatus, out int linked);

                if (linked == 0)
                {
                    throw new InvalidOperationException($"Failed to link {BackendName} shader program: {_gl.GetProgramInfoLog(program)}");
                }

                return program;
            }
            catch
            {
                if (program != 0)
                {
                    _gl.DeleteProgram(program);
                }

                throw;
            }
            finally
            {
                if (vertexShader != 0)
                {
                    _gl.DeleteShader(vertexShader);
                }

                if (fragmentShader != 0)
                {
                    _gl.DeleteShader(fragmentShader);
                }
            }
        }

        private uint CompileShader(uint shaderType, string source)
        {
            uint shader = _gl.CreateShader(shaderType);
            try
            {
                _gl.ShaderSource(shader, source);
                _gl.CompileShader(shader);
                _gl.GetShaderiv(shader, OpenGlConstants.CompileStatus, out int compiled);
                if (compiled == 0)
                {
                    string shaderKind = shaderType == OpenGlConstants.VertexShader ? "vertex" : "fragment";
                    throw new InvalidOperationException($"Failed to compile {BackendName} {shaderKind} shader: {_gl.GetShaderInfoLog(shader)}");
                }

                return shader;
            }
            catch
            {
                if (shader != 0)
                {
                    _gl.DeleteShader(shader);
                }

                throw;
            }
        }

        private string GetVertexShaderSource(ShaderDialect dialect)
        {
            if (dialect == ShaderDialect.Core)
            {
                return """
                    #version 150
                    in vec2 a_position;
                    in vec2 a_uv;
                    in vec4 a_color;
                    out vec2 v_uv;
                    out vec4 v_color;
                    void main() {
                        v_uv = a_uv;
                        v_color = a_color;
                        gl_Position = vec4(a_position, 0.0, 1.0);
                    }
                    """;
            }

            return """
                attribute vec2 a_position;
                attribute vec2 a_uv;
                attribute vec4 a_color;
                varying vec2 v_uv;
                varying vec4 v_color;
                void main() {
                    v_uv = a_uv;
                    v_color = a_color;
                    gl_Position = vec4(a_position, 0.0, 1.0);
                }
                """;
        }

        private string GetFragmentShaderSource(ShaderDialect dialect)
        {
            if (dialect == ShaderDialect.Core)
            {
                return """
                    #version 150
                    in vec2 v_uv;
                    in vec4 v_color;
                    uniform sampler2D u_texture;
                    out vec4 frag_color;
                    void main() {
                        vec4 color = texture(u_texture, v_uv) * v_color;
                        color.rgb *= v_color.a;
                        frag_color = color;
                    }
                    """;
            }

            return """
                #ifdef GL_ES
                precision mediump float;
                #endif
                varying vec2 v_uv;
                varying vec4 v_color;
                uniform sampler2D u_texture;
                void main() {
                    vec4 color = texture2D(u_texture, v_uv) * v_color;
                    color.rgb *= v_color.a;
                    gl_FragColor = color;
                }
                """;
        }
    }
}
