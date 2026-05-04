using System.Collections.Generic;
using System.Linq;

namespace EffectViewer.Rendering.OpenGl
{
    public sealed class OpenGlTextureCache : ITextureCache
    {
        private readonly Dictionary<RenderTextureRef, CachedTexture> _textures = [];

        public bool TryGetTexture(RenderTextureRef texture, int revision, out int handle)
        {
            if (_textures.TryGetValue(texture, out CachedTexture cached) && cached.Revision == revision)
            {
                handle = cached.Handle;
                return true;
            }

            handle = 0;
            return false;
        }

        public void SetTexture(RenderTextureRef texture, int handle, int revision)
        {
            _textures[texture] = new CachedTexture(handle, revision);
        }

        public void Invalidate(RenderTextureRef texture)
        {
            _textures.Remove(texture);
        }

        public int Remove(RenderTextureRef texture)
        {
            if (_textures.Remove(texture, out CachedTexture cached))
            {
                return cached.Handle;
            }

            return 0;
        }

        public void Clear()
        {
            _textures.Clear();
        }

        public IReadOnlyCollection<int> Handles => _textures.Values.Select(texture => texture.Handle).ToArray();

        private readonly record struct CachedTexture(int Handle, int Revision);
    }
}
