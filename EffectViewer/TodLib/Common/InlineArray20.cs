using System.Runtime.CompilerServices;

namespace EffectViewer.TodLib.Common
{
    [InlineArray(20)]
    public struct InlineArray20<T>
    {
        private T _buffer;
    }

    [InlineArray(38)]
    public struct InlineArray38<T>
    {
        private T _buffer;
    }
}
