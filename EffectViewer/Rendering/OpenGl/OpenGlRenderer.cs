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

        private uint _program;
        private uint _vertexBuffer;
        private int _positionLocation = -1;
        private int _uvLocation = -1;
        private int _colorLocation = -1;
        private int _textureLocation = -1;
        private bool _canDrawSprites;
        private readonly OpenGlTextureCache _textureCache = new();
        private ITextureSource _textureSource = new GeneratedTextureSource();

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
            uint[] buffers = new uint[1];
            _gl.GenBuffers(1, buffers);
            _vertexBuffer = buffers[0];

            _positionLocation = _gl.GetAttribLocation(_program, "a_position");
            _uvLocation = _gl.GetAttribLocation(_program, "a_uv");
            _colorLocation = _gl.GetAttribLocation(_program, "a_color");
            _textureLocation = _gl.GetUniformLocation(_program, "u_texture");

            _canDrawSprites = _program != 0 &&
                              _vertexBuffer != 0 &&
                              _positionLocation >= 0 &&
                              _uvLocation >= 0 &&
                              _colorLocation >= 0 &&
                              _textureLocation >= 0;
            IsInitialized = true;
        }

        public void Render(RenderFrame frame, int framebuffer, int width, int height)
        {
            if (!IsInitialized)
            {
                return;
            }

            _gl.BindFramebuffer(OpenGlConstants.Framebuffer, (uint)framebuffer);

            _gl.Viewport(0, 0, Math.Max(1, width), Math.Max(1, height));
            Vector4 clear = frame.ClearColor;
            _gl.ClearColor(clear.X, clear.Y, clear.Z, clear.W);
            _gl.Enable(OpenGlConstants.Blend);
            _gl.BlendFunc(OpenGlConstants.SrcAlpha, OpenGlConstants.OneMinusSrcAlpha);

            _gl.Clear(OpenGlConstants.ColorBufferBit);
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

            if (_gl != null && _program != 0)
            {
                _gl.DeleteProgram(_program);
            }

            _program = 0;
            _vertexBuffer = 0;
            _positionLocation = -1;
            _uvLocation = -1;
            _colorLocation = -1;
            _textureLocation = -1;
            _canDrawSprites = false;
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

            float[] vertices = new float[sprites.Count * 6 * FloatsPerVertex];
            int offset = 0;
            foreach (RenderSpriteCommand sprite in sprites)
            {
                AppendSprite(vertices, ref offset, sprite, width, height, ViewZoom, ViewPan);
            }

            UploadVertices(vertices);

            int firstVertex = 0;
            foreach (RenderSpriteCommand sprite in sprites)
            {
                SetBlendMode(sprite.BlendMode);
                BindTexture(sprite.Texture);
                _gl.DrawArrays(OpenGlConstants.Triangles, firstVertex, 6);
                firstVertex += 6;
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

                float[] vertices = new float[mesh.Vertices.Count * FloatsPerVertex];
                int offset = 0;
                foreach (RenderVertex vertex in mesh.Vertices)
                {
                    AppendVertex(
                        vertices,
                        ref offset,
                        ToClipX(ApplyViewX(vertex.Position.X, ViewZoom, ViewPan), width),
                        ToClipY(ApplyViewY(vertex.Position.Y, ViewZoom, ViewPan), height),
                        vertex.Uv.X,
                        vertex.Uv.Y,
                        vertex.Color);
                }

                UploadVertices(vertices);
                SetBlendMode(mesh.BlendMode);
                BindTexture(mesh.Texture);
                _gl.DrawArrays(OpenGlConstants.Triangles, 0, mesh.Vertices.Count);
            }
        }

        private void SetBlendMode(RenderBlendMode blendMode)
        {
            if (blendMode == RenderBlendMode.Additive)
            {
                _gl.BlendFunc(OpenGlConstants.SrcAlpha, OpenGlConstants.One);
            }
            else
            {
                _gl.BlendFunc(OpenGlConstants.SrcAlpha, OpenGlConstants.OneMinusSrcAlpha);
            }
        }

        private void UploadVertices(float[] vertices)
        {
            _gl.UseProgram(_program);
            _gl.Uniform1i(_textureLocation, 0);
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

        private void BindTexture(RenderTextureRef texture)
        {
            if (!_textureCache.TryGetTexture(texture, out int handle))
            {
                handle = CreateTexture(texture);
                if (handle == 0)
                {
                    return;
                }

                _textureCache.SetTexture(texture, handle);
            }

            _gl.ActiveTexture(OpenGlConstants.Texture0);
            _gl.BindTexture(OpenGlConstants.Texture2D, (uint)handle);
        }

        private int CreateTexture(RenderTextureRef texture)
        {
            if (!_textureSource.TryLoad(texture, out TextureUploadData data) ||
                data.Width <= 0 ||
                data.Height <= 0 ||
                data.RgbaPixels.Length < data.Width * data.Height * 4)
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
            GCHandle pixelsHandle = GCHandle.Alloc(data.RgbaPixels, GCHandleType.Pinned);
            try
            {
                _gl.TexImage2D(
                    OpenGlConstants.Texture2D,
                    0,
                    (int)OpenGlConstants.Rgba,
                    data.Width,
                    data.Height,
                    0,
                    OpenGlConstants.Rgba,
                    OpenGlConstants.UnsignedByte,
                    pixelsHandle.AddrOfPinnedObject());
            }
            finally
            {
                pixelsHandle.Free();
            }

            return (int)handle;
        }

        private static void AppendSprite(float[] vertices, ref int offset, RenderSpriteCommand sprite, int width, int height, float zoom, Vector2 pan)
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

            AppendVertex(vertices, ref offset, left, top, uv.X, uv.Y, color);
            AppendVertex(vertices, ref offset, right, top, uv.Z, uv.Y, color);
            AppendVertex(vertices, ref offset, right, bottom, uv.Z, uv.W, color);
            AppendVertex(vertices, ref offset, left, top, uv.X, uv.Y, color);
            AppendVertex(vertices, ref offset, right, bottom, uv.Z, uv.W, color);
            AppendVertex(vertices, ref offset, left, bottom, uv.X, uv.W, color);
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
            uint vertexShader = CompileShader(OpenGlConstants.VertexShader, GetVertexShaderSource());
            uint fragmentShader = CompileShader(OpenGlConstants.FragmentShader, GetFragmentShaderSource());

            if (vertexShader == 0 || fragmentShader == 0)
            {
                return 0;
            }

            uint program = _gl.CreateProgram();
            _gl.AttachShader(program, vertexShader);
            _gl.AttachShader(program, fragmentShader);
            _gl.LinkProgram(program);
            _gl.GetProgramiv(program, OpenGlConstants.LinkStatus, out int linked);

            _gl.DeleteShader(vertexShader);
            _gl.DeleteShader(fragmentShader);

            if (linked == 0)
            {
                throw new InvalidOperationException($"Failed to link {BackendName} shader program: {_gl.GetProgramInfoLog(program)}");
            }

            return program;
        }

        private uint CompileShader(uint shaderType, string source)
        {
            uint shader = _gl.CreateShader(shaderType);
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

        private string GetVertexShaderSource()
        {
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

        private string GetFragmentShaderSource()
        {
            return """
                #ifdef GL_ES
                precision mediump float;
                #endif
                varying vec2 v_uv;
                varying vec4 v_color;
                uniform sampler2D u_texture;
                void main() {
                    gl_FragColor = texture2D(u_texture, v_uv) * v_color;
                }
                """;
        }
    }
}
