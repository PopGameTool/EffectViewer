using System;
using System.Collections.Generic;
using EffectViewer.Assets;

namespace EffectViewer.Projects
{
    public sealed class AssetIndex
    {
        private readonly Dictionary<string, ImageAsset> _images = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, FontAsset> _fonts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ReanimAsset> _reanims = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, EffectAsset> _particles = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, EffectAsset> _trails = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ShowcaseAsset> _showcases = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, ImageAsset> Images => _images;
        public IReadOnlyDictionary<string, FontAsset> Fonts => _fonts;
        public IReadOnlyDictionary<string, ReanimAsset> Reanims => _reanims;
        public IReadOnlyDictionary<string, EffectAsset> Particles => _particles;
        public IReadOnlyDictionary<string, EffectAsset> Trails => _trails;
        public IReadOnlyDictionary<string, ShowcaseAsset> Showcases => _showcases;

        public AssetIndex(ProjectManifest manifest)
        {
            foreach (ImageAsset asset in manifest.Images)
            {
                if (!string.IsNullOrWhiteSpace(asset.Id))
                {
                    _images[asset.Id] = asset;
                }
            }

            AddEffects(manifest.Fonts, _fonts);
            AddReanims(manifest.Reanims, _reanims);
            AddEffects(manifest.Particles, _particles);
            AddEffects(manifest.Trails, _trails);

            foreach (ShowcaseAsset asset in manifest.Showcases)
            {
                if (!string.IsNullOrWhiteSpace(asset.Id))
                {
                    _showcases[asset.Id] = asset;
                }
            }
        }

        public bool TryGetImage(string id, out ImageAsset asset)
        {
            asset = null;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            if (_images.TryGetValue(id, out asset))
            {
                return true;
            }

            const string reanimPrefix = "IMAGE_REANIM_";
            if (id.StartsWith(reanimPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string fallbackId = "IMAGE_" + id[reanimPrefix.Length..];
                return _images.TryGetValue(fallbackId, out asset);
            }

            return false;
        }

        public bool TryGetFont(string id, out FontAsset asset)
        {
            asset = null;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            if (_fonts.TryGetValue(id, out asset))
            {
                return true;
            }

            const string fontPrefix = "FONT_";
            if (id.StartsWith(fontPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string fallbackId = id[fontPrefix.Length..];
                return _fonts.TryGetValue(fallbackId, out asset);
            }

            return _fonts.TryGetValue(fontPrefix + id, out asset);
        }

        private static void AddEffects<TAsset>(IEnumerable<TAsset> source, Dictionary<string, TAsset> target)
            where TAsset : EffectAsset
        {
            foreach (TAsset asset in source)
            {
                if (!string.IsNullOrWhiteSpace(asset.Id))
                {
                    target[asset.Id] = asset;
                }
            }
        }

        private static void AddReanims(IEnumerable<ReanimAsset> source, Dictionary<string, ReanimAsset> target)
        {
            foreach (ReanimAsset asset in source)
            {
                if (!string.IsNullOrWhiteSpace(asset.Id))
                {
                    target[asset.Id] = asset;
                }
            }
        }
    }
}
