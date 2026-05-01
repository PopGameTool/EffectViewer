using System.Collections.Generic;
using EffectViewer.Projects;

namespace EffectViewer.Assets
{
    public sealed class ReanimAsset : EffectAsset
    {
        public List<ReanimTween> Tweens { get; set; } = [];
    }

    public sealed class ReanimTween
    {
        public int TrackIndex { get; set; }
        public string TrackName { get; set; } = string.Empty;
        public int StartFrame { get; set; }
        public int EndFrame { get; set; }
        public List<string> Properties { get; set; } = [];
    }
}
