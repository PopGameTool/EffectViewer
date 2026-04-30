using System.Collections.ObjectModel;
using EffectViewer.Projects;
using EffectViewer.Runtime.Showcase;
using EffectViewer.TodLib.Common;

namespace EffectViewer.Runtime
{
    public sealed class EffectWorld : System.IDisposable
    {
        public EffectProject Project { get; private set; }
        public ObservableCollection<SceneObject> Objects { get; } = [];
        public ShowcaseScene ShowcaseScene { get; private set; }

        public void LoadProject(EffectProject project)
        {
            Project = project;
            Objects.Clear();
            ShowcaseScene?.Dispose();
            ShowcaseScene = null;
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
            ShowcaseScene?.Clear();
        }

        public ShowcaseScene BeginShowcase()
        {
            Objects.Clear();
            ShowcaseScene?.Dispose();
            ShowcaseScene = new ShowcaseScene(Project);
            return ShowcaseScene;
        }

        public void Dispose()
        {
            ShowcaseScene?.Dispose();
            ShowcaseScene = null;
        }
    }
}
