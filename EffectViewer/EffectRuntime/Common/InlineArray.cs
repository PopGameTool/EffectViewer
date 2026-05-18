using System.Runtime.CompilerServices;

namespace EffectViewer.EffectRuntime.Common
{
    [InlineArray(20)]
    public struct InlineArray20<T>
    {
        private T _buffer;
    }

    [InlineArray(24)]
    public struct InlineArray24<T>
    {
        private T _buffer;
    }

    [InlineArray(38)]
    public struct InlineArray38<T>
    {
        private T _buffer;
    }

    [InlineArray(256)]
    public struct InlineArray256<T>
    {
        private T _buffer;
    }
}
