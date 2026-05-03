using System;

namespace EffectViewer.Rendering.Gl
{
    public interface IEffectGlInterface
    {
        EffectGlApi Api { get; }
        string BackendName { get; }
        bool CanClear { get; }
        bool CanDraw { get; }
        bool SupportsVertexArrayObjects { get; }

        void Viewport(int x, int y, int width, int height);
        void ClearColor(float red, float green, float blue, float alpha);
        void Clear(uint mask);
        void BindFramebuffer(uint target, uint framebuffer);
        void Enable(uint cap);
        void BlendFunc(uint sourceFactor, uint destinationFactor);
        void BlendFuncSeparate(uint sourceRgbFactor, uint destinationRgbFactor, uint sourceAlphaFactor, uint destinationAlphaFactor);
        uint CreateShader(uint shaderType);
        void ShaderSource(uint shader, string source);
        void CompileShader(uint shader);
        void GetShaderiv(uint shader, uint parameterName, out int value);
        string GetShaderInfoLog(uint shader);
        uint CreateProgram();
        void AttachShader(uint program, uint shader);
        void LinkProgram(uint program);
        void GetProgramiv(uint program, uint parameterName, out int value);
        string GetProgramInfoLog(uint program);
        void UseProgram(uint program);
        void DeleteShader(uint shader);
        void DeleteProgram(uint program);
        void GenBuffers(int count, uint[] buffers);
        void DeleteBuffers(int count, uint[] buffers);
        void BindBuffer(uint target, uint buffer);
        void GenVertexArrays(int count, uint[] arrays);
        void DeleteVertexArrays(int count, uint[] arrays);
        void BindVertexArray(uint array);
        void BufferData(uint target, IntPtr size, IntPtr data, uint usage);
        int GetAttribLocation(uint program, string name);
        void EnableVertexAttribArray(uint index);
        void VertexAttribPointer(uint index, int size, uint type, bool normalized, int stride, IntPtr pointer);
        void DrawArrays(uint mode, int first, int count);
        void GenTextures(int count, uint[] textures);
        void DeleteTextures(int count, uint[] textures);
        void ActiveTexture(uint texture);
        void BindTexture(uint target, uint texture);
        void TexParameteri(uint target, uint parameterName, int parameter);
        void PixelStorei(uint parameterName, int parameter);
        void TexImage2D(uint target, int level, int internalFormat, int width, int height, int border, uint format, uint type, IntPtr pixels);
        int GetUniformLocation(uint program, string name);
        void Uniform1i(int location, int value);
        uint GetError();
    }
}
