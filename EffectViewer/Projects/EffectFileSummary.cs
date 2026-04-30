using System.Collections.Generic;

namespace EffectViewer.Projects
{
    public sealed class EffectFileSummary
    {
        public bool IsLoaded { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public int TrackCount { get; set; }
        public int FrameCount { get; set; }
        public float Fps { get; set; }
        public int EmitterCount { get; set; }
        public int FieldCount { get; set; }
        public int MaxPoints { get; set; }
        public float MinPointDistance { get; set; }
        public IReadOnlyList<string> ImageIds { get; set; } = [];
        public IReadOnlyList<string> ResolvedImageIds { get; set; } = [];
        public IReadOnlyList<ImageReferenceResolution> ImageResolutions { get; set; } = [];
        public IReadOnlyList<string> MissingImageIds { get; set; } = [];
    }
}
