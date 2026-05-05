using System.Collections.Generic;
using System.Linq;
using EffectViewer.Rendering.TextureUpload;

namespace EffectViewer.Rendering.OpenGl
{
    public sealed class OpenGlTextureCache : ITextureCache
    {
        private readonly Dictionary<RenderTextureRef, CachedTextureSet> _textures = [];

        public bool TryGetTexture(RenderTextureRef texture, int revision, out int handle)
        {
            if (_textures.TryGetValue(texture, out CachedTextureSet cached) &&
                cached.Revision == revision &&
                cached.Set.Handles.Count == 1)
            {
                handle = cached.Set.Handles[0];
                return true;
            }

            handle = 0;
            return false;
        }

        public void SetTexture(RenderTextureRef texture, int handle, int revision)
        {
            TextureTileLayout layout = TextureTileLayout.Create(1, 1, 1);
            _textures[texture] = new CachedTextureSet(new OpenGlTextureSet(layout, [handle]), revision);
        }

        public bool TryGetTextureSet(RenderTextureRef texture, int revision, out OpenGlTextureSet set)
        {
            if (_textures.TryGetValue(texture, out CachedTextureSet cached) && cached.Revision == revision)
            {
                set = cached.Set;
                return true;
            }

            set = null;
            return false;
        }

        public void SetTextureSet(RenderTextureRef texture, OpenGlTextureSet set, int revision)
        {
            _textures[texture] = new CachedTextureSet(set, revision);
        }

        public void Invalidate(RenderTextureRef texture)
        {
            _textures.Remove(texture);
        }

        public int Remove(RenderTextureRef texture)
        {
            if (_textures.Remove(texture, out CachedTextureSet cached) && cached.Set.Handles.Count == 1)
            {
                return cached.Set.Handles[0];
            }

            return 0;
        }

        public IReadOnlyCollection<int> RemoveTextureSet(RenderTextureRef texture)
        {
            if (_textures.Remove(texture, out CachedTextureSet cached))
            {
                return cached.Set.Handles.ToArray();
            }

            return [];
        }

        public void Clear()
        {
            _textures.Clear();
        }

        public IReadOnlyCollection<int> Handles => _textures.Values
            .SelectMany(texture => texture.Set.Handles)
            .ToArray();

        private readonly record struct CachedTextureSet(OpenGlTextureSet Set, int Revision);
    }

    public sealed class OpenGlTextureSet
    {
        public OpenGlTextureSet(TextureTileLayout layout, IReadOnlyList<int> handles)
        {
            Layout = layout;
            Handles = handles;
        }

        public TextureTileLayout Layout { get; }
        public IReadOnlyList<int> Handles { get; }

        public int GetHandle(TextureTile tile)
        {
            return tile.Index >= 0 && tile.Index < Handles.Count ? Handles[tile.Index] : 0;
        }
    }
}
