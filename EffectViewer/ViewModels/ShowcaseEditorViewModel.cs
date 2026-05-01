using System.Collections.ObjectModel;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
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
        public ObservableCollection<ShowcaseLogEntry> Logs { get; } = [];
        public ObservableCollection<SceneObject> SceneObjects { get; } = [];
        public override bool SupportsSave => !string.IsNullOrWhiteSpace(Path) && Path != "User script";
        public override bool SupportsFileExport => SupportsSave;
        public override string ExportPath => SupportsSave ? Path : string.Empty;

        [ObservableProperty]
        private string _scriptText;

        [ObservableProperty]
        private string _status = "Ready";

        [ObservableProperty]
        private ShowcaseLogEntry _selectedLog;

        public string SelectedLogDetail => SelectedLog?.Detail ?? "No log selected.";

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
            _scriptText = LoadScriptText(asset, project);
            MarkClean();
        }

        partial void OnScriptTextChanged(string value)
        {
            MarkDirty();
        }

        public override async Task SaveAsync(EffectProjectService projectService, EffectProject project)
        {
            if (!SupportsSave)
            {
                await base.SaveAsync(projectService, project);
                return;
            }

            string fullPath = ResolveShowcasePath(project, Path);
            string directory = System.IO.Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fullPath, ScriptText ?? string.Empty);
            AcceptSavedState();
        }

        [RelayCommand]
        private void RunScript()
        {
            Logs.Clear();
            SceneObjects.Clear();
            SelectedLog = null;

            LuaRunResult result = _luaHost.Run(ScriptText, AddRuntimeLog);

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
                AddRuntimeLog(Status);
            }
        }

        partial void OnSelectedLogChanged(ShowcaseLogEntry value)
        {
            OnPropertyChanged(nameof(SelectedLogDetail));
        }

        private void AddRuntimeLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            ShowcaseLogEntry entry = ShowcaseLogEntry.FromMessage(message);
            if (Dispatcher.UIThread.CheckAccess())
            {
                AddLogEntry(entry);
            }
            else
            {
                Dispatcher.UIThread.Post(() => AddLogEntry(entry));
            }
        }

        private void AddLogEntry(ShowcaseLogEntry entry)
        {
            Logs.Add(entry);
            SelectedLog ??= entry;
        }

        private static string LoadScriptText(ShowcaseAsset asset, EffectProject project)
        {
            string fullPath = ResolveShowcasePath(project, asset.Path);
            if (!string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath))
            {
                try
                {
                    return File.ReadAllText(fullPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return """
                scene.clear()

                local body = effect.reanim("sample_reanim", 400, 300)
                local fire = effect.particle("fire_burst", 420, 280)
                local slash = effect.trail("sword_slash", 0, 0)

                local t = 0

                function update(dt)
                    t = t + dt
                    body:set_position(400 + math.sin(t * 2) * 40, 300)
                    fire:set_scale(0.8 + math.sin(t * 3) * 0.2)
                    slash:clear_points()
                    slash:add_point(330, 320)
                    slash:add_point(470, 280 + math.sin(t * 4) * 40)
                end

                function draw(g)
                    body:draw(g)
                    fire:draw(g)
                    slash:draw(g)
                end

                effect.log("showcase initialized")
                """;
        }

        private static string ResolveShowcasePath(EffectProject project, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path == "User script")
            {
                return string.Empty;
            }

            string normalizedPath = path.Replace('\\', System.IO.Path.DirectorySeparatorChar).Replace('/', System.IO.Path.DirectorySeparatorChar);
            return System.IO.Path.IsPathRooted(normalizedPath) || string.IsNullOrWhiteSpace(project?.RootPath)
                ? normalizedPath
                : System.IO.Path.Combine(project.RootPath, normalizedPath);
        }
    }
}
