using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EffectViewer.Projects;
using EffectViewer.Rendering;
using EffectViewer.Runtime;
using EffectViewer.Runtime.Lua;

namespace EffectViewer.ViewModels
{
    public sealed partial class ShowcaseEditorViewModel : EditorViewModelBase
    {
        private readonly LuaHost _luaHost;
        private readonly EffectProject _project;

        public string AssetId { get; }
        public string Path { get; }
        public ObservableCollection<string> Logs { get; } = [];
        public ObservableCollection<SceneObject> SceneObjects { get; } = [];

        [ObservableProperty]
        private string _scriptText;

        [ObservableProperty]
        private string _status = "Ready";

        public ShowcaseEditorViewModel(LuaHost luaHost, EffectProject project)
            : this(new ShowcaseAsset { Id = "Showcases", Path = "User script" }, luaHost, project)
        {
        }

        public ShowcaseEditorViewModel(ShowcaseAsset asset, LuaHost luaHost, EffectProject project)
            : base(asset.Id, EffectAssetKind.Showcase)
        {
            AssetId = asset.Id;
            Path = asset.Path;
            _luaHost = luaHost;
            _project = project;
            PreviewFrame = EffectPreviewFrameBuilder.BuildPlaceholder(EffectAssetKind.Showcase, asset.Id);
            TextureSource = new Rendering.TextureUpload.ProjectTextureSource(project);
            _scriptText = """
                scene.clear()
                effect.reanim("sample_reanim", 400, 300)
                effect.particle("fire_burst", 420, 280)
                effect.trail("sword_slash", 0, 0)
                effect.log("showcase initialized")
                """;
        }

        [RelayCommand]
        private void RunScript()
        {
            Logs.Clear();
            SceneObjects.Clear();

            LuaRunResult result = _luaHost.Run(ScriptText);
            foreach (string log in result.Logs)
            {
                Logs.Add(log);
            }

            foreach (SceneObject sceneObject in result.SceneObjects)
            {
                SceneObjects.Add(sceneObject);
            }

            if (PreviewFrameProvider is System.IDisposable disposableProvider)
            {
                disposableProvider.Dispose();
            }

            PreviewFrameProvider = result.Success
                ? result.FrameProvider
                : null;

            Status = result.Success
                ? $"Ran script, created {result.SceneObjects.Count} object(s)."
                : "Script failed.";

            if (!Logs.Any())
            {
                Logs.Add(Status);
            }
        }
    }
}
