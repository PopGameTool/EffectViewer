using System;
using System.Runtime.InteropServices;

namespace EffectViewer.Rendering.Gl
{
    public abstract class ProcAddressEffectGlInterface : IEffectGlInterface
    {
        private delegate void GlViewportDelegate(int x, int y, int width, int height);
        private delegate void GlClearColorDelegate(float red, float green, float blue, float alpha);
        private delegate void GlClearDelegate(uint mask);
        private delegate void GlBindFramebufferDelegate(uint target, uint framebuffer);
        private delegate void GlEnableDelegate(uint cap);
        private delegate void GlBlendFuncDelegate(uint sourceFactor, uint destinationFactor);
        private delegate uint GlCreateShaderDelegate(uint shaderType);
        private delegate void GlShaderSourceDelegate(uint shader, int count, IntPtr source, IntPtr length);
        private delegate void GlCompileShaderDelegate(uint shader);
        private delegate void GlGetShaderivDelegate(uint shader, uint parameterName, IntPtr value);
        private delegate void GlGetShaderInfoLogDelegate(uint shader, int bufferSize, IntPtr length, IntPtr infoLog);
        private delegate uint GlCreateProgramDelegate();
        private delegate void GlAttachShaderDelegate(uint program, uint shader);
        private delegate void GlLinkProgramDelegate(uint program);
        private delegate void GlGetProgramivDelegate(uint program, uint parameterName, IntPtr value);
        private delegate void GlGetProgramInfoLogDelegate(uint program, int bufferSize, IntPtr length, IntPtr infoLog);
        private delegate void GlUseProgramDelegate(uint program);
        private delegate void GlDeleteShaderDelegate(uint shader);
        private delegate void GlDeleteProgramDelegate(uint program);
        private delegate void GlGenBuffersDelegate(int count, IntPtr buffers);
        private delegate void GlDeleteBuffersDelegate(int count, IntPtr buffers);
        private delegate void GlBindBufferDelegate(uint target, uint buffer);
        private delegate void GlGenVertexArraysDelegate(int count, IntPtr arrays);
        private delegate void GlDeleteVertexArraysDelegate(int count, IntPtr arrays);
        private delegate void GlBindVertexArrayDelegate(uint array);
        private delegate void GlBufferDataDelegate(uint target, IntPtr size, IntPtr data, uint usage);
        private delegate int GlGetAttribLocationDelegate(uint program, IntPtr name);
        private delegate void GlEnableVertexAttribArrayDelegate(uint index);
        private delegate void GlVertexAttribPointerDelegate(uint index, int size, uint type, bool normalized, int stride, IntPtr pointer);
        private delegate void GlDrawArraysDelegate(uint mode, int first, int count);
        private delegate void GlGenTexturesDelegate(int count, IntPtr textures);
        private delegate void GlDeleteTexturesDelegate(int count, IntPtr textures);
        private delegate void GlActiveTextureDelegate(uint texture);
        private delegate void GlBindTextureDelegate(uint target, uint texture);
        private delegate void GlTexParameteriDelegate(uint target, uint parameterName, int parameter);
        private delegate void GlPixelStoreiDelegate(uint parameterName, int parameter);
        private delegate void GlTexImage2DDelegate(uint target, int level, int internalFormat, int width, int height, int border, uint format, uint type, IntPtr pixels);
        private delegate int GlGetUniformLocationDelegate(uint program, IntPtr name);
        private delegate void GlUniform1iDelegate(int location, int value);
        private delegate uint GlGetErrorDelegate();

        private readonly GlViewportDelegate _viewport;
        private readonly GlClearColorDelegate _clearColor;
        private readonly GlClearDelegate _clear;
        private readonly GlBindFramebufferDelegate _bindFramebuffer;
        private readonly GlEnableDelegate _enable;
        private readonly GlBlendFuncDelegate _blendFunc;
        private readonly GlCreateShaderDelegate _createShader;
        private readonly GlShaderSourceDelegate _shaderSource;
        private readonly GlCompileShaderDelegate _compileShader;
        private readonly GlGetShaderivDelegate _getShaderiv;
        private readonly GlGetShaderInfoLogDelegate _getShaderInfoLog;
        private readonly GlCreateProgramDelegate _createProgram;
        private readonly GlAttachShaderDelegate _attachShader;
        private readonly GlLinkProgramDelegate _linkProgram;
        private readonly GlGetProgramivDelegate _getProgramiv;
        private readonly GlGetProgramInfoLogDelegate _getProgramInfoLog;
        private readonly GlUseProgramDelegate _useProgram;
        private readonly GlDeleteShaderDelegate _deleteShader;
        private readonly GlDeleteProgramDelegate _deleteProgram;
        private readonly GlGenBuffersDelegate _genBuffers;
        private readonly GlDeleteBuffersDelegate _deleteBuffers;
        private readonly GlBindBufferDelegate _bindBuffer;
        private readonly GlGenVertexArraysDelegate _genVertexArrays;
        private readonly GlDeleteVertexArraysDelegate _deleteVertexArrays;
        private readonly GlBindVertexArrayDelegate _bindVertexArray;
        private readonly GlBufferDataDelegate _bufferData;
        private readonly GlGetAttribLocationDelegate _getAttribLocation;
        private readonly GlEnableVertexAttribArrayDelegate _enableVertexAttribArray;
        private readonly GlVertexAttribPointerDelegate _vertexAttribPointer;
        private readonly GlDrawArraysDelegate _drawArrays;
        private readonly GlGenTexturesDelegate _genTextures;
        private readonly GlDeleteTexturesDelegate _deleteTextures;
        private readonly GlActiveTextureDelegate _activeTexture;
        private readonly GlBindTextureDelegate _bindTexture;
        private readonly GlTexParameteriDelegate _texParameteri;
        private readonly GlPixelStoreiDelegate _pixelStorei;
        private readonly GlTexImage2DDelegate _texImage2D;
        private readonly GlGetUniformLocationDelegate _getUniformLocation;
        private readonly GlUniform1iDelegate _uniform1i;
        private readonly GlGetErrorDelegate _getError;

        protected ProcAddressEffectGlInterface(
            EffectGlApi api,
            string backendName,
            Func<string, IntPtr> getProcAddress)
        {
            ArgumentNullException.ThrowIfNull(getProcAddress);

            Api = api;
            BackendName = backendName;

            _viewport = Load<GlViewportDelegate>(getProcAddress, "glViewport");
            _clearColor = Load<GlClearColorDelegate>(getProcAddress, "glClearColor");
            _clear = Load<GlClearDelegate>(getProcAddress, "glClear");
            _bindFramebuffer = LoadAny<GlBindFramebufferDelegate>(getProcAddress, "glBindFramebuffer", "glBindFramebufferEXT");
            _enable = Load<GlEnableDelegate>(getProcAddress, "glEnable");
            _blendFunc = Load<GlBlendFuncDelegate>(getProcAddress, "glBlendFunc");
            _createShader = Load<GlCreateShaderDelegate>(getProcAddress, "glCreateShader");
            _shaderSource = Load<GlShaderSourceDelegate>(getProcAddress, "glShaderSource");
            _compileShader = Load<GlCompileShaderDelegate>(getProcAddress, "glCompileShader");
            _getShaderiv = Load<GlGetShaderivDelegate>(getProcAddress, "glGetShaderiv");
            _getShaderInfoLog = Load<GlGetShaderInfoLogDelegate>(getProcAddress, "glGetShaderInfoLog");
            _createProgram = Load<GlCreateProgramDelegate>(getProcAddress, "glCreateProgram");
            _attachShader = Load<GlAttachShaderDelegate>(getProcAddress, "glAttachShader");
            _linkProgram = Load<GlLinkProgramDelegate>(getProcAddress, "glLinkProgram");
            _getProgramiv = Load<GlGetProgramivDelegate>(getProcAddress, "glGetProgramiv");
            _getProgramInfoLog = Load<GlGetProgramInfoLogDelegate>(getProcAddress, "glGetProgramInfoLog");
            _useProgram = Load<GlUseProgramDelegate>(getProcAddress, "glUseProgram");
            _deleteShader = Load<GlDeleteShaderDelegate>(getProcAddress, "glDeleteShader");
            _deleteProgram = Load<GlDeleteProgramDelegate>(getProcAddress, "glDeleteProgram");
            _genBuffers = Load<GlGenBuffersDelegate>(getProcAddress, "glGenBuffers");
            _deleteBuffers = Load<GlDeleteBuffersDelegate>(getProcAddress, "glDeleteBuffers");
            _bindBuffer = Load<GlBindBufferDelegate>(getProcAddress, "glBindBuffer");
            _genVertexArrays = LoadAny<GlGenVertexArraysDelegate>(getProcAddress, "glGenVertexArrays", "glGenVertexArraysAPPLE", "glGenVertexArraysOES");
            _deleteVertexArrays = LoadAny<GlDeleteVertexArraysDelegate>(getProcAddress, "glDeleteVertexArrays", "glDeleteVertexArraysAPPLE", "glDeleteVertexArraysOES");
            _bindVertexArray = LoadAny<GlBindVertexArrayDelegate>(getProcAddress, "glBindVertexArray", "glBindVertexArrayAPPLE", "glBindVertexArrayOES");
            _bufferData = Load<GlBufferDataDelegate>(getProcAddress, "glBufferData");
            _getAttribLocation = Load<GlGetAttribLocationDelegate>(getProcAddress, "glGetAttribLocation");
            _enableVertexAttribArray = Load<GlEnableVertexAttribArrayDelegate>(getProcAddress, "glEnableVertexAttribArray");
            _vertexAttribPointer = Load<GlVertexAttribPointerDelegate>(getProcAddress, "glVertexAttribPointer");
            _drawArrays = Load<GlDrawArraysDelegate>(getProcAddress, "glDrawArrays");
            _genTextures = Load<GlGenTexturesDelegate>(getProcAddress, "glGenTextures");
            _deleteTextures = Load<GlDeleteTexturesDelegate>(getProcAddress, "glDeleteTextures");
            _activeTexture = Load<GlActiveTextureDelegate>(getProcAddress, "glActiveTexture");
            _bindTexture = Load<GlBindTextureDelegate>(getProcAddress, "glBindTexture");
            _texParameteri = Load<GlTexParameteriDelegate>(getProcAddress, "glTexParameteri");
            _pixelStorei = Load<GlPixelStoreiDelegate>(getProcAddress, "glPixelStorei");
            _texImage2D = Load<GlTexImage2DDelegate>(getProcAddress, "glTexImage2D");
            _getUniformLocation = Load<GlGetUniformLocationDelegate>(getProcAddress, "glGetUniformLocation");
            _uniform1i = Load<GlUniform1iDelegate>(getProcAddress, "glUniform1i");
            _getError = Load<GlGetErrorDelegate>(getProcAddress, "glGetError");

            CanClear = _viewport != null && _clearColor != null && _clear != null;
            CanDraw = CanClear &&
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
        }

        public EffectGlApi Api { get; }
        public string BackendName { get; }
        public bool CanClear { get; }
        public bool CanDraw { get; }
        public bool SupportsVertexArrayObjects => _genVertexArrays != null &&
                                                  _deleteVertexArrays != null &&
                                                  _bindVertexArray != null;

        public void Viewport(int x, int y, int width, int height) => _viewport(x, y, width, height);
        public void ClearColor(float red, float green, float blue, float alpha) => _clearColor(red, green, blue, alpha);
        public void Clear(uint mask) => _clear(mask);
        public void BindFramebuffer(uint target, uint framebuffer) => _bindFramebuffer?.Invoke(target, framebuffer);
        public void Enable(uint cap) => _enable?.Invoke(cap);
        public void BlendFunc(uint sourceFactor, uint destinationFactor) => _blendFunc?.Invoke(sourceFactor, destinationFactor);
        public uint CreateShader(uint shaderType) => _createShader(shaderType);
        public unsafe void ShaderSource(uint shader, string source)
        {
            byte[] sourceBytes = System.Text.Encoding.UTF8.GetBytes(source);
            fixed (byte* sourcePtr = sourceBytes)
            {
                IntPtr sourceAddress = new(sourcePtr);
                IntPtr* sources = stackalloc IntPtr[1];
                int* lengths = stackalloc int[1];
                sources[0] = sourceAddress;
                lengths[0] = sourceBytes.Length;
                _shaderSource(shader, 1, new IntPtr(sources), new IntPtr(lengths));
            }
        }
        public void CompileShader(uint shader) => _compileShader(shader);
        public unsafe void GetShaderiv(uint shader, uint parameterName, out int value)
        {
            int localValue = 0;
            _getShaderiv(shader, parameterName, new IntPtr(&localValue));
            value = localValue;
        }

        public string GetShaderInfoLog(uint shader) => GetInfoLog(_getShaderInfoLog, shader);
        public uint CreateProgram() => _createProgram();
        public void AttachShader(uint program, uint shader) => _attachShader(program, shader);
        public void LinkProgram(uint program) => _linkProgram(program);
        public unsafe void GetProgramiv(uint program, uint parameterName, out int value)
        {
            int localValue = 0;
            _getProgramiv(program, parameterName, new IntPtr(&localValue));
            value = localValue;
        }

        public string GetProgramInfoLog(uint program) => GetInfoLog(_getProgramInfoLog, program);
        public void UseProgram(uint program) => _useProgram(program);
        public void DeleteShader(uint shader) => _deleteShader?.Invoke(shader);
        public void DeleteProgram(uint program) => _deleteProgram?.Invoke(program);
        public void GenBuffers(int count, uint[] buffers) => InvokeWithPinnedArray(_genBuffers, count, buffers);
        public void DeleteBuffers(int count, uint[] buffers) => InvokeWithPinnedArray(_deleteBuffers, count, buffers);
        public void BindBuffer(uint target, uint buffer) => _bindBuffer(target, buffer);
        public void GenVertexArrays(int count, uint[] arrays) => InvokeWithPinnedArray(_genVertexArrays, count, arrays);
        public void DeleteVertexArrays(int count, uint[] arrays) => InvokeWithPinnedArray(_deleteVertexArrays, count, arrays);
        public void BindVertexArray(uint array) => _bindVertexArray?.Invoke(array);
        public void BufferData(uint target, IntPtr size, IntPtr data, uint usage) => _bufferData(target, size, data, usage);
        public int GetAttribLocation(uint program, string name) => InvokeWithUtf8Name(_getAttribLocation, program, name);
        public void EnableVertexAttribArray(uint index) => _enableVertexAttribArray(index);
        public void VertexAttribPointer(uint index, int size, uint type, bool normalized, int stride, IntPtr pointer) => _vertexAttribPointer(index, size, type, normalized, stride, pointer);
        public void DrawArrays(uint mode, int first, int count) => _drawArrays(mode, first, count);
        public void GenTextures(int count, uint[] textures) => InvokeWithPinnedArray(_genTextures, count, textures);
        public void DeleteTextures(int count, uint[] textures) => InvokeWithPinnedArray(_deleteTextures, count, textures);
        public void ActiveTexture(uint texture) => _activeTexture(texture);
        public void BindTexture(uint target, uint texture) => _bindTexture(target, texture);
        public void TexParameteri(uint target, uint parameterName, int parameter) => _texParameteri(target, parameterName, parameter);
        public void PixelStorei(uint parameterName, int parameter) => _pixelStorei?.Invoke(parameterName, parameter);
        public void TexImage2D(uint target, int level, int internalFormat, int width, int height, int border, uint format, uint type, IntPtr pixels) =>
            _texImage2D(target, level, internalFormat, width, height, border, format, type, pixels);
        public int GetUniformLocation(uint program, string name) => InvokeWithUtf8Name(_getUniformLocation, program, name);
        public void Uniform1i(int location, int value) => _uniform1i(location, value);
        public uint GetError() => _getError?.Invoke() ?? 0;

        private static unsafe string GetInfoLog<T>(T getInfoLog, uint handle)
            where T : Delegate
        {
            if (getInfoLog == null)
            {
                return string.Empty;
            }

            byte[] buffer = new byte[4096];
            int length = 0;
            fixed (byte* bufferPtr = buffer)
            {
                if (getInfoLog is GlGetShaderInfoLogDelegate shaderInfoLog)
                {
                    shaderInfoLog(handle, buffer.Length, new IntPtr(&length), new IntPtr(bufferPtr));
                }
                else if (getInfoLog is GlGetProgramInfoLogDelegate programInfoLog)
                {
                    programInfoLog(handle, buffer.Length, new IntPtr(&length), new IntPtr(bufferPtr));
                }
            }

            length = Math.Clamp(length, 0, buffer.Length);
            return length <= 0 ? string.Empty : System.Text.Encoding.UTF8.GetString(buffer, 0, length).TrimEnd('\0');
        }

        private static void InvokeWithPinnedArray<T>(T action, int count, uint[] values)
            where T : Delegate
        {
            if (action == null)
            {
                return;
            }

            GCHandle handle = GCHandle.Alloc(values, GCHandleType.Pinned);
            try
            {
                if (action is GlGenBuffersDelegate genBuffers)
                {
                    genBuffers(count, handle.AddrOfPinnedObject());
                }
                else if (action is GlDeleteBuffersDelegate deleteBuffers)
                {
                    deleteBuffers(count, handle.AddrOfPinnedObject());
                }
                else if (action is GlGenVertexArraysDelegate genVertexArrays)
                {
                    genVertexArrays(count, handle.AddrOfPinnedObject());
                }
                else if (action is GlDeleteVertexArraysDelegate deleteVertexArrays)
                {
                    deleteVertexArrays(count, handle.AddrOfPinnedObject());
                }
                else if (action is GlGenTexturesDelegate genTextures)
                {
                    genTextures(count, handle.AddrOfPinnedObject());
                }
                else if (action is GlDeleteTexturesDelegate deleteTextures)
                {
                    deleteTextures(count, handle.AddrOfPinnedObject());
                }
            }
            finally
            {
                handle.Free();
            }
        }

        private static unsafe int InvokeWithUtf8Name<T>(T function, uint program, string name)
            where T : Delegate
        {
            byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(name + '\0');
            fixed (byte* namePtr = nameBytes)
            {
                if (function is GlGetAttribLocationDelegate getAttribLocation)
                {
                    return getAttribLocation(program, new IntPtr(namePtr));
                }

                if (function is GlGetUniformLocationDelegate getUniformLocation)
                {
                    return getUniformLocation(program, new IntPtr(namePtr));
                }
            }

            return -1;
        }

        private static T Load<T>(Func<string, IntPtr> getProcAddress, string name)
            where T : Delegate
        {
            IntPtr address = getProcAddress(name);
            return address == IntPtr.Zero
                ? null
                : Marshal.GetDelegateForFunctionPointer<T>(address);
        }

        private static T LoadAny<T>(Func<string, IntPtr> getProcAddress, params string[] names)
            where T : Delegate
        {
            foreach (string name in names)
            {
                T function = Load<T>(getProcAddress, name);
                if (function != null)
                {
                    return function;
                }
            }

            return null;
        }
    }
}
