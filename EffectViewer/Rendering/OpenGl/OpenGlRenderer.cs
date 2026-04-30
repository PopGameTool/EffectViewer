using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Avalonia.OpenGL;
using EffectViewer.Rendering.TextureUpload;

namespace EffectViewer.Rendering.OpenGl
{
    public sealed class OpenGlRenderer
    {
        private const int FloatsPerVertex = 8;

        private delegate void GlViewportDelegate(int x, int y, int width, int height);
        private delegate void GlClearColorDelegate(float red, float green, float blue, float alpha);
        private delegate void GlClearDelegate(uint mask);
        private delegate void GlBindFramebufferDelegate(uint target, uint framebuffer);
        private delegate void GlEnableDelegate(uint cap);
        private delegate void GlBlendFuncDelegate(uint sourceFactor, uint destinationFactor);
        private delegate uint GlCreateShaderDelegate(uint shaderType);
        private delegate void GlShaderSourceDelegate(uint shader, int count, string[] source, int[] length);
        private delegate void GlCompileShaderDelegate(uint shader);
        private delegate void GlGetShaderivDelegate(uint shader, uint parameterName, out int value);
        private delegate uint GlCreateProgramDelegate();
        private delegate void GlAttachShaderDelegate(uint program, uint shader);
        private delegate void GlLinkProgramDelegate(uint program);
        private delegate void GlGetProgramivDelegate(uint program, uint parameterName, out int value);
        private delegate void GlUseProgramDelegate(uint program);
        private delegate void GlDeleteShaderDelegate(uint shader);
        private delegate void GlDeleteProgramDelegate(uint program);
        private delegate void GlGenBuffersDelegate(int count, uint[] buffers);
        private delegate void GlDeleteBuffersDelegate(int count, uint[] buffers);
        private delegate void GlBindBufferDelegate(uint target, uint buffer);
        private delegate void GlBufferDataDelegate(uint target, IntPtr size, float[] data, uint usage);
        private delegate int GlGetAttribLocationDelegate(uint program, string name);
        private delegate void GlEnableVertexAttribArrayDelegate(uint index);
        private delegate void GlVertexAttribPointerDelegate(uint index, int size, uint type, bool normalized, int stride, IntPtr pointer);
        private delegate void GlDrawArraysDelegate(uint mode, int first, int count);
        private delegate void GlGenTexturesDelegate(int count, uint[] textures);
        private delegate void GlDeleteTexturesDelegate(int count, uint[] textures);
        private delegate void GlActiveTextureDelegate(uint texture);
        private delegate void GlBindTextureDelegate(uint target, uint texture);
        private delegate void GlTexParameteriDelegate(uint target, uint parameterName, int parameter);
        private delegate void GlTexImage2DDelegate(uint target, int level, int internalFormat, int width, int height, int border, uint format, uint type, byte[] pixels);
        private delegate int GlGetUniformLocationDelegate(uint program, string name);
        private delegate void GlUniform1iDelegate(int location, int value);

        private GlViewportDelegate _viewport;
        private GlClearColorDelegate _clearColor;
        private GlClearDelegate _clear;
        private GlBindFramebufferDelegate _bindFramebuffer;
        private GlEnableDelegate _enable;
        private GlBlendFuncDelegate _blendFunc;
        private GlCreateShaderDelegate _createShader;
        private GlShaderSourceDelegate _shaderSource;
        private GlCompileShaderDelegate _compileShader;
        private GlGetShaderivDelegate _getShaderiv;
        private GlCreateProgramDelegate _createProgram;
        private GlAttachShaderDelegate _attachShader;
        private GlLinkProgramDelegate _linkProgram;
        private GlGetProgramivDelegate _getProgramiv;
        private GlUseProgramDelegate _useProgram;
        private GlDeleteShaderDelegate _deleteShader;
        private GlDeleteProgramDelegate _deleteProgram;
        private GlGenBuffersDelegate _genBuffers;
        private GlDeleteBuffersDelegate _deleteBuffers;
        private GlBindBufferDelegate _bindBuffer;
        private GlBufferDataDelegate _bufferData;
        private GlGetAttribLocationDelegate _getAttribLocation;
        private GlEnableVertexAttribArrayDelegate _enableVertexAttribArray;
        private GlVertexAttribPointerDelegate _vertexAttribPointer;
        private GlDrawArraysDelegate _drawArrays;
        private GlGenTexturesDelegate _genTextures;
        private GlDeleteTexturesDelegate _deleteTextures;
        private GlActiveTextureDelegate _activeTexture;
        private GlBindTextureDelegate _bindTexture;
        private GlTexParameteriDelegate _texParameteri;
        private GlTexImage2DDelegate _texImage2D;
        private GlGetUniformLocationDelegate _getUniformLocation;
        private GlUniform1iDelegate _uniform1i;

        private uint _program;
        private uint _vertexBuffer;
        private int _positionLocation = -1;
        private int _uvLocation = -1;
        private int _colorLocation = -1;
        private int _textureLocation = -1;
        private bool _canDrawSprites;
        private readonly OpenGlTextureCache _textureCache = new();
        private ITextureSource _textureSource = new GeneratedTextureSource();

        public string BackendName => "OpenGL";
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

        public void Initialize(GlInterface gl)
        {
            LoadFunctions(gl);

            bool hasCore = _viewport != null &&
                           _clearColor != null &&
                           _clear != null &&
                           _createShader != null &&
                           _shaderSource != null &&
                           _compileShader != null &&
                           _getShaderiv != null &&
                           _createProgram != null &&
                           _attachShader != null &&
                           _linkProgram != null &&
                           _getProgramiv != null &&
                           _useProgram != null &&
                           _genBuffers != null &&
                           _bindBuffer != null &&
                           _bufferData != null &&
                           _getAttribLocation != null &&
                           _enableVertexAttribArray != null &&
                           _vertexAttribPointer != null &&
                           _drawArrays != null &&
                           _genTextures != null &&
                           _activeTexture != null &&
                           _bindTexture != null &&
                           _texParameteri != null &&
                           _texImage2D != null &&
                           _getUniformLocation != null &&
                           _uniform1i != null;

            if (!hasCore)
            {
                IsInitialized = _viewport != null && _clearColor != null && _clear != null;
                return;
            }

            _program = CreateProgram();
            uint[] buffers = new uint[1];
            _genBuffers(1, buffers);
            _vertexBuffer = buffers[0];

            _positionLocation = _getAttribLocation(_program, "a_position");
            _uvLocation = _getAttribLocation(_program, "a_uv");
            _colorLocation = _getAttribLocation(_program, "a_color");
            _textureLocation = _getUniformLocation(_program, "u_texture");

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

            if (_bindFramebuffer != null)
            {
                _bindFramebuffer(OpenGlConstants.Framebuffer, (uint)framebuffer);
            }

            _viewport(0, 0, Math.Max(1, width), Math.Max(1, height));
            Vector4 clear = frame.ClearColor;
            _clearColor(clear.X, clear.Y, clear.Z, clear.W);

            if (_enable != null && _blendFunc != null)
            {
                _enable(OpenGlConstants.Blend);
                _blendFunc(OpenGlConstants.SrcAlpha, OpenGlConstants.OneMinusSrcAlpha);
            }

            _clear(OpenGlConstants.ColorBufferBit);
            DrawSprites(frame.Sprites, width, height);
            DrawMeshes(frame.Meshes, width, height);
        }

        public void Deinitialize()
        {
            if (_deleteBuffers != null && _vertexBuffer != 0)
            {
                _deleteBuffers(1, [_vertexBuffer]);
            }

            if (_deleteProgram != null && _program != 0)
            {
                _deleteProgram(_program);
            }

            _program = 0;
            _vertexBuffer = 0;
            _positionLocation = -1;
            _uvLocation = -1;
            _colorLocation = -1;
            _textureLocation = -1;
            _canDrawSprites = false;
            _textureCache.Clear();

            _viewport = null;
            _clearColor = null;
            _clear = null;
            _bindFramebuffer = null;
            _enable = null;
            _blendFunc = null;
            _createShader = null;
            _shaderSource = null;
            _compileShader = null;
            _getShaderiv = null;
            _createProgram = null;
            _attachShader = null;
            _linkProgram = null;
            _getProgramiv = null;
            _useProgram = null;
            _deleteShader = null;
            _deleteProgram = null;
            _genBuffers = null;
            _deleteBuffers = null;
            _bindBuffer = null;
            _bufferData = null;
            _getAttribLocation = null;
            _enableVertexAttribArray = null;
            _vertexAttribPointer = null;
            _drawArrays = null;
            _genTextures = null;
            _deleteTextures = null;
            _activeTexture = null;
            _bindTexture = null;
            _texParameteri = null;
            _texImage2D = null;
            _getUniformLocation = null;
            _uniform1i = null;
            IsInitialized = false;
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
                _drawArrays(OpenGlConstants.Triangles, firstVertex, 6);
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
                _drawArrays(OpenGlConstants.Triangles, 0, mesh.Vertices.Count);
            }
        }

        private void SetBlendMode(RenderBlendMode blendMode)
        {
            if (_blendFunc == null)
            {
                return;
            }

            if (blendMode == RenderBlendMode.Additive)
            {
                _blendFunc(OpenGlConstants.SrcAlpha, OpenGlConstants.One);
            }
            else
            {
                _blendFunc(OpenGlConstants.SrcAlpha, OpenGlConstants.OneMinusSrcAlpha);
            }
        }

        private void UploadVertices(float[] vertices)
        {
            _useProgram(_program);
            _uniform1i(_textureLocation, 0);
            _bindBuffer(OpenGlConstants.ArrayBuffer, _vertexBuffer);
            _bufferData(
                OpenGlConstants.ArrayBuffer,
                new IntPtr(vertices.Length * sizeof(float)),
                vertices,
                OpenGlConstants.StreamDraw);

            int stride = FloatsPerVertex * sizeof(float);
            _enableVertexAttribArray((uint)_positionLocation);
            _vertexAttribPointer((uint)_positionLocation, 2, OpenGlConstants.Float, false, stride, IntPtr.Zero);

            if (_uvLocation >= 0)
            {
                _enableVertexAttribArray((uint)_uvLocation);
                _vertexAttribPointer((uint)_uvLocation, 2, OpenGlConstants.Float, false, stride, new IntPtr(2 * sizeof(float)));
            }

            _enableVertexAttribArray((uint)_colorLocation);
            _vertexAttribPointer((uint)_colorLocation, 4, OpenGlConstants.Float, false, stride, new IntPtr(4 * sizeof(float)));
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

            _activeTexture(OpenGlConstants.Texture0);
            _bindTexture(OpenGlConstants.Texture2D, (uint)handle);
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
            _genTextures(1, handles);
            uint handle = handles[0];
            if (handle == 0)
            {
                return 0;
            }

            _bindTexture(OpenGlConstants.Texture2D, handle);
            _texParameteri(OpenGlConstants.Texture2D, OpenGlConstants.TextureMinFilter, OpenGlConstants.Linear);
            _texParameteri(OpenGlConstants.Texture2D, OpenGlConstants.TextureMagFilter, OpenGlConstants.Linear);
            _texParameteri(OpenGlConstants.Texture2D, OpenGlConstants.TextureWrapS, OpenGlConstants.ClampToEdge);
            _texParameteri(OpenGlConstants.Texture2D, OpenGlConstants.TextureWrapT, OpenGlConstants.ClampToEdge);
            _texImage2D(
                OpenGlConstants.Texture2D,
                0,
                (int)OpenGlConstants.Rgba,
                data.Width,
                data.Height,
                0,
                OpenGlConstants.Rgba,
                OpenGlConstants.UnsignedByte,
                data.RgbaPixels);

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
            uint vertexShader = CompileShader(OpenGlConstants.VertexShader, """
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
                """);

            uint fragmentShader = CompileShader(OpenGlConstants.FragmentShader, """
                precision mediump float;
                varying vec2 v_uv;
                varying vec4 v_color;
                uniform sampler2D u_texture;
                void main() {
                    gl_FragColor = texture2D(u_texture, v_uv) * v_color;
                }
                """);

            if (vertexShader == 0 || fragmentShader == 0)
            {
                return 0;
            }

            uint program = _createProgram();
            _attachShader(program, vertexShader);
            _attachShader(program, fragmentShader);
            _linkProgram(program);
            _getProgramiv(program, OpenGlConstants.LinkStatus, out int linked);

            if (_deleteShader != null)
            {
                _deleteShader(vertexShader);
                _deleteShader(fragmentShader);
            }

            return linked == 0 ? 0 : program;
        }

        private uint CompileShader(uint shaderType, string source)
        {
            uint shader = _createShader(shaderType);
            string[] sources = [source];
            int[] lengths = [source.Length];
            _shaderSource(shader, 1, sources, lengths);
            _compileShader(shader);
            _getShaderiv(shader, OpenGlConstants.CompileStatus, out int compiled);
            return compiled == 0 ? 0 : shader;
        }

        private void LoadFunctions(GlInterface gl)
        {
            _viewport = Load<GlViewportDelegate>(gl, "glViewport");
            _clearColor = Load<GlClearColorDelegate>(gl, "glClearColor");
            _clear = Load<GlClearDelegate>(gl, "glClear");
            _bindFramebuffer = Load<GlBindFramebufferDelegate>(gl, "glBindFramebuffer");
            _enable = Load<GlEnableDelegate>(gl, "glEnable");
            _blendFunc = Load<GlBlendFuncDelegate>(gl, "glBlendFunc");
            _createShader = Load<GlCreateShaderDelegate>(gl, "glCreateShader");
            _shaderSource = Load<GlShaderSourceDelegate>(gl, "glShaderSource");
            _compileShader = Load<GlCompileShaderDelegate>(gl, "glCompileShader");
            _getShaderiv = Load<GlGetShaderivDelegate>(gl, "glGetShaderiv");
            _createProgram = Load<GlCreateProgramDelegate>(gl, "glCreateProgram");
            _attachShader = Load<GlAttachShaderDelegate>(gl, "glAttachShader");
            _linkProgram = Load<GlLinkProgramDelegate>(gl, "glLinkProgram");
            _getProgramiv = Load<GlGetProgramivDelegate>(gl, "glGetProgramiv");
            _useProgram = Load<GlUseProgramDelegate>(gl, "glUseProgram");
            _deleteShader = Load<GlDeleteShaderDelegate>(gl, "glDeleteShader");
            _deleteProgram = Load<GlDeleteProgramDelegate>(gl, "glDeleteProgram");
            _genBuffers = Load<GlGenBuffersDelegate>(gl, "glGenBuffers");
            _deleteBuffers = Load<GlDeleteBuffersDelegate>(gl, "glDeleteBuffers");
            _bindBuffer = Load<GlBindBufferDelegate>(gl, "glBindBuffer");
            _bufferData = Load<GlBufferDataDelegate>(gl, "glBufferData");
            _getAttribLocation = Load<GlGetAttribLocationDelegate>(gl, "glGetAttribLocation");
            _enableVertexAttribArray = Load<GlEnableVertexAttribArrayDelegate>(gl, "glEnableVertexAttribArray");
            _vertexAttribPointer = Load<GlVertexAttribPointerDelegate>(gl, "glVertexAttribPointer");
            _drawArrays = Load<GlDrawArraysDelegate>(gl, "glDrawArrays");
            _genTextures = Load<GlGenTexturesDelegate>(gl, "glGenTextures");
            _deleteTextures = Load<GlDeleteTexturesDelegate>(gl, "glDeleteTextures");
            _activeTexture = Load<GlActiveTextureDelegate>(gl, "glActiveTexture");
            _bindTexture = Load<GlBindTextureDelegate>(gl, "glBindTexture");
            _texParameteri = Load<GlTexParameteriDelegate>(gl, "glTexParameteri");
            _texImage2D = Load<GlTexImage2DDelegate>(gl, "glTexImage2D");
            _getUniformLocation = Load<GlGetUniformLocationDelegate>(gl, "glGetUniformLocation");
            _uniform1i = Load<GlUniform1iDelegate>(gl, "glUniform1i");
        }

        private static T Load<T>(GlInterface gl, string name)
            where T : Delegate
        {
            IntPtr address = gl.GetProcAddress(name);
            return address == IntPtr.Zero
                ? null
                : Marshal.GetDelegateForFunctionPointer<T>(address);
        }
    }
}
