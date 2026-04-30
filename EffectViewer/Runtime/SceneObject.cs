using EffectViewer.Projects;

namespace EffectViewer.Runtime
{
    public sealed class SceneObject
    {
        public EffectAssetKind Kind { get; }
        public string Id { get; }
        public double X { get; set; }
        public double Y { get; set; }

        public SceneObject(EffectAssetKind kind, string id, double x, double y)
        {
            Kind = kind;
            Id = id;
            X = x;
            Y = y;
        }
    }
}
