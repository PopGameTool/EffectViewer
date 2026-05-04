using System.Collections.Generic;
using EffectViewer.Assets;

namespace EffectViewer.Projects
{
    public sealed class ProjectManifest
    {
        public int Version { get; set; } = 2;
        public string Name { get; set; } = "Untitled Effect Project";
        public List<ImageAsset> Images { get; set; } = [];
        public List<FontAsset> Fonts { get; set; } = [];
        public List<ReanimAsset> Reanims { get; set; } = [];
        public List<EffectAsset> Particles { get; set; } = [];
        public List<EffectAsset> Trails { get; set; } = [];
        public List<ShowcaseAsset> Showcases { get; set; } = [];
    }
}
