using System;

namespace EffectViewer.Rendering.Gl
{
    public abstract unsafe class ProcAddressEffectGlInterface : IEffectGlInterface
    {
        private readonly delegate* unmanaged<int, int, int, int, void> _viewport;
        private readonly delegate* unmanaged<float, float, float, float, void> _clearColor;
        private readonly delegate* unmanaged<uint, void> _clear;
        private readonly delegate* unmanaged<uint, uint, void> _bindFramebuffer;
        private readonly delegate* unmanaged<uint, void> _enable;
        private readonly delegate* unmanaged<uint, uint, void> _blendFunc;
        private readonly delegate* unmanaged<uint, uint, uint, uint, void> _blendFuncSeparate;
        private readonly delegate* unmanaged<uint, uint> _createShader;
        private readonly delegate* unmanaged<uint, int, IntPtr, IntPtr, void> _shaderSource;
        private readonly delegate* unmanaged<uint, void> _compileShader;
        private readonly delegate* unmanaged<uint, uint, IntPtr, void> _getShaderiv;
        private readonly delegate* unmanaged<uint, int, IntPtr, IntPtr, void> _getShaderInfoLog;
        private readonly delegate* unmanaged<uint> _createProgram;
        private readonly delegate* unmanaged<uint, uint, void> _attachShader;
        private readonly delegate* unmanaged<uint, void> _linkProgram;
        private readonly delegate* unmanaged<uint, uint, IntPtr, void> _getProgramiv;
        private readonly delegate* unmanaged<uint, int, IntPtr, IntPtr, void> _getProgramInfoLog;
        private readonly delegate* unmanaged<uint, void> _useProgram;
        private readonly delegate* unmanaged<uint, void> _deleteShader;
        private readonly delegate* unmanaged<uint, void> _deleteProgram;
        private readonly delegate* unmanaged<int, IntPtr, void> _genBuffers;
        private readonly delegate* unmanaged<int, IntPtr, void> _deleteBuffers;
        private readonly delegate* unmanaged<uint, uint, void> _bindBuffer;
        private readonly delegate* unmanaged<int, IntPtr, void> _genVertexArrays;
        private readonly delegate* unmanaged<int, IntPtr, void> _deleteVertexArrays;
        private readonly delegate* unmanaged<uint, void> _bindVertexArray;
        private readonly delegate* unmanaged<uint, IntPtr, IntPtr, uint, void> _bufferData;
        private readonly delegate* unmanaged<uint, IntPtr, int> _getAttribLocation;
        private readonly delegate* unmanaged<uint, void> _enableVertexAttribArray;
        private readonly delegate* unmanaged<uint, int, uint, byte, int, IntPtr, void> _vertexAttribPointer;
        private readonly delegate* unmanaged<uint, int, int, void> _drawArrays;
        private readonly delegate* unmanaged<int, IntPtr, void> _genTextures;
        private readonly delegate* unmanaged<int, IntPtr, void> _deleteTextures;
        private readonly delegate* unmanaged<uint, void> _activeTexture;
        private readonly delegate* unmanaged<uint, uint, void> _bindTexture;
        private readonly delegate* unmanaged<uint, uint, int, void> _texParameteri;
        private readonly delegate* unmanaged<uint, int, void> _pixelStorei;
        private readonly delegate* unmanaged<uint, int, int, int, int, int, uint, uint, IntPtr, void> _texImage2D;
        private readonly delegate* unmanaged<uint, IntPtr, int> _getUniformLocation;
        private readonly delegate* unmanaged<int, int, void> _uniform1i;
        private readonly delegate* unmanaged<uint> _getError;

        protected ProcAddressEffectGlInterface(
            EffectGlApi api,
            string backendName,
            Func<string, IntPtr> getProcAddress)
        {
            ArgumentNullException.ThrowIfNull(getProcAddress);

            Api = api;
            BackendName = backendName;

            _viewport = (delegate* unmanaged<int, int, int, int, void>)Load(getProcAddress, "glViewport");
            _clearColor = (delegate* unmanaged<float, float, float, float, void>)Load(getProcAddress, "glClearColor");
            _clear = (delegate* unmanaged<uint, void>)Load(getProcAddress, "glClear");
            _bindFramebuffer = (delegate* unmanaged<uint, uint, void>)LoadAny(getProcAddress, "glBindFramebuffer", "glBindFramebufferEXT");
            _enable = (delegate* unmanaged<uint, void>)Load(getProcAddress, "glEnable");
            _blendFunc = (delegate* unmanaged<uint, uint, void>)Load(getProcAddress, "glBlendFunc");
            _blendFuncSeparate = (delegate* unmanaged<uint, uint, uint, uint, void>)LoadAny(getProcAddress, "glBlendFuncSeparate", "glBlendFuncSeparateOES");
            _createShader = (delegate* unmanaged<uint, uint>)Load(getProcAddress, "glCreateShader");
            _shaderSource = (delegate* unmanaged<uint, int, IntPtr, IntPtr, void>)Load(getProcAddress, "glShaderSource");
            _compileShader = (delegate* unmanaged<uint, void>)Load(getProcAddress, "glCompileShader");
            _getShaderiv = (delegate* unmanaged<uint, uint, IntPtr, void>)Load(getProcAddress, "glGetShaderiv");
            _getShaderInfoLog = (delegate* unmanaged<uint, int, IntPtr, IntPtr, void>)Load(getProcAddress, "glGetShaderInfoLog");
            _createProgram = (delegate* unmanaged<uint>)Load(getProcAddress, "glCreateProgram");
            _attachShader = (delegate* unmanaged<uint, uint, void>)Load(getProcAddress, "glAttachShader");
            _linkProgram = (delegate* unmanaged<uint, void>)Load(getProcAddress, "glLinkProgram");
            _getProgramiv = (delegate* unmanaged<uint, uint, IntPtr, void>)Load(getProcAddress, "glGetProgramiv");
            _getProgramInfoLog = (delegate* unmanaged<uint, int, IntPtr, IntPtr, void>)Load(getProcAddress, "glGetProgramInfoLog");
            _useProgram = (delegate* unmanaged<uint, void>)Load(getProcAddress, "glUseProgram");
            _deleteShader = (delegate* unmanaged<uint, void>)Load(getProcAddress, "glDeleteShader");
            _deleteProgram = (delegate* unmanaged<uint, void>)Load(getProcAddress, "glDeleteProgram");
            _genBuffers = (delegate* unmanaged<int, IntPtr, void>)Load(getProcAddress, "glGenBuffers");
            _deleteBuffers = (delegate* unmanaged<int, IntPtr, void>)Load(getProcAddress, "glDeleteBuffers");
            _bindBuffer = (delegate* unmanaged<uint, uint, void>)Load(getProcAddress, "glBindBuffer");
            _genVertexArrays = (delegate* unmanaged<int, IntPtr, void>)LoadAny(getProcAddress, "glGenVertexArrays", "glGenVertexArraysAPPLE", "glGenVertexArraysOES");
            _deleteVertexArrays = (delegate* unmanaged<int, IntPtr, void>)LoadAny(getProcAddress, "glDeleteVertexArrays", "glDeleteVertexArraysAPPLE", "glDeleteVertexArraysOES");
            _bindVertexArray = (delegate* unmanaged<uint, void>)LoadAny(getProcAddress, "glBindVertexArray", "glBindVertexArrayAPPLE", "glBindVertexArrayOES");
            _bufferData = (delegate* unmanaged<uint, IntPtr, IntPtr, uint, void>)Load(getProcAddress, "glBufferData");
            _getAttribLocation = (delegate* unmanaged<uint, IntPtr, int>)Load(getProcAddress, "glGetAttribLocation");
            _enableVertexAttribArray = (delegate* unmanaged<uint, void>)Load(getProcAddress, "glEnableVertexAttribArray");
            _vertexAttribPointer = (delegate* unmanaged<uint, int, uint, byte, int, IntPtr, void>)Load(getProcAddress, "glVertexAttribPointer");
            _drawArrays = (delegate* unmanaged<uint, int, int, void>)Load(getProcAddress, "glDrawArrays");
            _genTextures = (delegate* unmanaged<int, IntPtr, void>)Load(getProcAddress, "glGenTextures");
            _deleteTextures = (delegate* unmanaged<int, IntPtr, void>)Load(getProcAddress, "glDeleteTextures");
            _activeTexture = (delegate* unmanaged<uint, void>)Load(getProcAddress, "glActiveTexture");
            _bindTexture = (delegate* unmanaged<uint, uint, void>)Load(getProcAddress, "glBindTexture");
            _texParameteri = (delegate* unmanaged<uint, uint, int, void>)Load(getProcAddress, "glTexParameteri");
            _pixelStorei = (delegate* unmanaged<uint, int, void>)Load(getProcAddress, "glPixelStorei");
            _texImage2D = (delegate* unmanaged<uint, int, int, int, int, int, uint, uint, IntPtr, void>)Load(getProcAddress, "glTexImage2D");
            _getUniformLocation = (delegate* unmanaged<uint, IntPtr, int>)Load(getProcAddress, "glGetUniformLocation");
            _uniform1i = (delegate* unmanaged<int, int, void>)Load(getProcAddress, "glUniform1i");
            _getError = (delegate* unmanaged<uint>)Load(getProcAddress, "glGetError");

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
        public void BindFramebuffer(uint target, uint framebuffer)
        {
            if (_bindFramebuffer != null)
            {
                _bindFramebuffer(target, framebuffer);
            }
        }

        public void Enable(uint cap)
        {
            if (_enable != null)
            {
                _enable(cap);
            }
        }

        public void BlendFunc(uint sourceFactor, uint destinationFactor)
        {
            if (_blendFunc != null)
            {
                _blendFunc(sourceFactor, destinationFactor);
            }
        }

        public void BlendFuncSeparate(uint sourceRgbFactor, uint destinationRgbFactor, uint sourceAlphaFactor, uint destinationAlphaFactor)
        {
            if (_blendFuncSeparate != null)
            {
                _blendFuncSeparate(sourceRgbFactor, destinationRgbFactor, sourceAlphaFactor, destinationAlphaFactor);
            }
            else
            {
                BlendFunc(sourceRgbFactor, destinationRgbFactor);
            }
        }

        public uint CreateShader(uint shaderType) => _createShader(shaderType);

        public void ShaderSource(uint shader, string source)
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

        public void GetShaderiv(uint shader, uint parameterName, out int value)
        {
            int localValue = 0;
            _getShaderiv(shader, parameterName, new IntPtr(&localValue));
            value = localValue;
        }

        public string GetShaderInfoLog(uint shader) => GetInfoLog(_getShaderInfoLog, shader);
        public uint CreateProgram() => _createProgram();
        public void AttachShader(uint program, uint shader) => _attachShader(program, shader);
        public void LinkProgram(uint program) => _linkProgram(program);

        public void GetProgramiv(uint program, uint parameterName, out int value)
        {
            int localValue = 0;
            _getProgramiv(program, parameterName, new IntPtr(&localValue));
            value = localValue;
        }

        public string GetProgramInfoLog(uint program) => GetInfoLog(_getProgramInfoLog, program);
        public void UseProgram(uint program) => _useProgram(program);

        public void DeleteShader(uint shader)
        {
            if (_deleteShader != null)
            {
                _deleteShader(shader);
            }
        }

        public void DeleteProgram(uint program)
        {
            if (_deleteProgram != null)
            {
                _deleteProgram(program);
            }
        }

        public void GenBuffers(int count, uint[] buffers) => InvokeWithPinnedArray(_genBuffers, count, buffers);
        public void DeleteBuffers(int count, uint[] buffers) => InvokeWithPinnedArray(_deleteBuffers, count, buffers);
        public void BindBuffer(uint target, uint buffer) => _bindBuffer(target, buffer);
        public void GenVertexArrays(int count, uint[] arrays) => InvokeWithPinnedArray(_genVertexArrays, count, arrays);
        public void DeleteVertexArrays(int count, uint[] arrays) => InvokeWithPinnedArray(_deleteVertexArrays, count, arrays);

        public void BindVertexArray(uint array)
        {
            if (_bindVertexArray != null)
            {
                _bindVertexArray(array);
            }
        }

        public void BufferData(uint target, IntPtr size, IntPtr data, uint usage) => _bufferData(target, size, data, usage);
        public int GetAttribLocation(uint program, string name) => InvokeWithUtf8Name(_getAttribLocation, program, name);
        public void EnableVertexAttribArray(uint index) => _enableVertexAttribArray(index);

        public void VertexAttribPointer(uint index, int size, uint type, bool normalized, int stride, IntPtr pointer) =>
            _vertexAttribPointer(index, size, type, normalized ? (byte)1 : (byte)0, stride, pointer);

        public void DrawArrays(uint mode, int first, int count) => _drawArrays(mode, first, count);
        public void GenTextures(int count, uint[] textures) => InvokeWithPinnedArray(_genTextures, count, textures);
        public void DeleteTextures(int count, uint[] textures) => InvokeWithPinnedArray(_deleteTextures, count, textures);
        public void ActiveTexture(uint texture) => _activeTexture(texture);
        public void BindTexture(uint target, uint texture) => _bindTexture(target, texture);
        public void TexParameteri(uint target, uint parameterName, int parameter) => _texParameteri(target, parameterName, parameter);

        public void PixelStorei(uint parameterName, int parameter)
        {
            if (_pixelStorei != null)
            {
                _pixelStorei(parameterName, parameter);
            }
        }

        public void TexImage2D(uint target, int level, int internalFormat, int width, int height, int border, uint format, uint type, IntPtr pixels) =>
            _texImage2D(target, level, internalFormat, width, height, border, format, type, pixels);

        public int GetUniformLocation(uint program, string name) => InvokeWithUtf8Name(_getUniformLocation, program, name);
        public void Uniform1i(int location, int value) => _uniform1i(location, value);

        public uint GetError()
        {
            return _getError == null ? 0 : _getError();
        }

        private static string GetInfoLog(delegate* unmanaged<uint, int, IntPtr, IntPtr, void> getInfoLog, uint handle)
        {
            if (getInfoLog == null)
            {
                return string.Empty;
            }

            byte[] buffer = new byte[4096];
            int length = 0;
            fixed (byte* bufferPtr = buffer)
            {
                getInfoLog(handle, buffer.Length, new IntPtr(&length), new IntPtr(bufferPtr));
            }

            length = Math.Clamp(length, 0, buffer.Length);
            return length <= 0 ? string.Empty : System.Text.Encoding.UTF8.GetString(buffer, 0, length).TrimEnd('\0');
        }

        private static void InvokeWithPinnedArray(delegate* unmanaged<int, IntPtr, void> action, int count, uint[] values)
        {
            if (action == null)
            {
                return;
            }

            fixed (uint* valuesPtr = values)
            {
                action(count, new IntPtr(valuesPtr));
            }
        }

        private static int InvokeWithUtf8Name(delegate* unmanaged<uint, IntPtr, int> function, uint program, string name)
        {
            if (function == null)
            {
                return -1;
            }

            byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(name + '\0');
            fixed (byte* namePtr = nameBytes)
            {
                return function(program, new IntPtr(namePtr));
            }
        }

        private static void* Load(Func<string, IntPtr> getProcAddress, string name)
        {
            IntPtr address = getProcAddress(name);
            return address == IntPtr.Zero ? null : (void*)address;
        }

        private static void* LoadAny(Func<string, IntPtr> getProcAddress, params string[] names)
        {
            foreach (string name in names)
            {
                void* function = Load(getProcAddress, name);
                if (function != null)
                {
                    return function;
                }
            }

            return null;
        }
    }
}
