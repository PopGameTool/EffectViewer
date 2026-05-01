using System.Collections.Generic;

namespace EffectViewer.Rendering.OpenGl
{
    public sealed class OpenGlTextureCache : ITextureCache
    {
        private readonly Dictionary<RenderTextureRef, int> _textures = [];

        public bool TryGetTexture(RenderTextureRef texture, out int handle)
        {
            return _textures.TryGetValue(texture, out handle);
        }

        public void SetTexture(RenderTextureRef texture, int handle)
        {
            _textures[texture] = handle;
        }

        public void Invalidate(RenderTextureRef texture)
        {
            _textures.Remove(texture);
        }

        public void Clear()
        {
            _textures.Clear();
        }

        public IReadOnlyCollection<int> Handles => _textures.Values;
    }
}
