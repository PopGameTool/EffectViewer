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

        public string AssetId { get; }
        public string Path { get; }
        public ObservableCollection<string> Logs { get; } = [];
        public ObservableCollection<SceneObject> SceneObjects { get; } = [];

        [ObservableProperty]
        private string _scriptText;

        [ObservableProperty]
        private string _status = "Ready";

        public ShowcaseEditorViewModel(ShowcaseAsset asset, LuaHost luaHost)
            : base(asset.Id, EffectAssetKind.Showcase)
        {
            AssetId = asset.Id;
            Path = asset.Path;
            _luaHost = luaHost;
            PreviewFrame = EffectPreviewFrameBuilder.BuildPlaceholder(EffectAssetKind.Showcase, asset.Id);
            _scriptText = """
                scene.clear()
                effect.reanim("sample_reanim", 400, 300)
                effect.particle("fire_burst", 420, 280)
                effect.trail("sword_slash")
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
