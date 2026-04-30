using System.Collections.ObjectModel;
using EffectViewer.Projects;
using EffectViewer.TodLib.Common;

namespace EffectViewer.Runtime
{
    public sealed class EffectWorld
    {
        public EffectProject Project { get; private set; }
        public ObservableCollection<SceneObject> Objects { get; } = [];

        public void LoadProject(EffectProject project)
        {
            Project = project;
            Objects.Clear();
            ResourceHandler.SetProvider(new ProjectResourceProvider(project));
        }

        public SceneObject AddObject(EffectAssetKind kind, string id, double x, double y)
        {
            SceneObject sceneObject = new(kind, id, x, y);
            Objects.Add(sceneObject);
            return sceneObject;
        }

        public void Clear()
        {
            Objects.Clear();
        }
    }
}
