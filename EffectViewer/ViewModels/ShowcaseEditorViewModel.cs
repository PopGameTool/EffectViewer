using System.Collections.ObjectModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EffectViewer.Localization;
using EffectViewer.Projects;
using EffectViewer.Rendering;
using EffectViewer.Runtime;
using EffectViewer.Runtime.Lua;
using EffectViewer.Runtime.Showcase;
using EffectViewer.TodLib.Common;

namespace EffectViewer.ViewModels
{
    public sealed partial class ShowcaseEditorViewModel : EditorViewModelBase
    {
        private readonly LuaHost _luaHost;
        private readonly EffectProject _project;
        private const string UserScriptPath = "User script";

        public string AssetId { get; }
        public string Path { get; }
        public string DisplayAssetId => IsUserScript ? Loc.Text("Showcase.Showcases") : AssetId;
        public string DisplayPath => IsUserScript ? Loc.Text("Showcase.UserScript") : Path;
        public ObservableCollection<ShowcaseLogEntry> Logs { get; } = [];
        public ObservableCollection<SceneObject> SceneObjects { get; } = [];
        public ObservableCollection<ShowcaseScriptAction> ScriptTemplates { get; } = [];
        public ObservableCollection<ShowcaseScriptAction> ScriptSnippets { get; } = [];
        public ObservableCollection<ShowcaseResourceReference> ResourceReferences { get; } = [];
        public IReadOnlyList<ShowcaseCompletionItem> CompletionItems { get; } = CreateCompletionItems().ToList();
        public override bool SupportsSave => !string.IsNullOrWhiteSpace(Path) && Path != UserScriptPath;
        public override bool SupportsFileExport => SupportsSave;
        public override string ExportPath => SupportsSave ? Path : string.Empty;

        [ObservableProperty]
        private string _scriptText;

        [ObservableProperty]
        private string _status = LocalizationManager.Instance.Text("Showcase.Ready");

        [ObservableProperty]
        private ShowcaseLogEntry _selectedLog;

        [ObservableProperty]
        private ShowcaseScriptAction _selectedScriptTemplate;

        [ObservableProperty]
        private ShowcaseScriptAction _selectedScriptSnippet;

        [ObservableProperty]
        private ShowcaseResourceReference _selectedResourceReference;

        public string SelectedLogDetail => SelectedLog?.Detail ?? Loc.Text("Showcase.NoLogSelected");

        public event EventHandler<ShowcaseScriptEditRequest> ScriptEditRequested;
        public event EventHandler<ShowcaseScriptNavigationRequest> ScriptNavigationRequested;

        public ShowcaseEditorViewModel(LuaHost luaHost, EffectProject project)
            : this(new ShowcaseAsset { Id = "Showcases", Path = UserScriptPath }, luaHost, project)
        {
        }

        public ShowcaseEditorViewModel(ShowcaseAsset asset, LuaHost luaHost, EffectProject project)
            : base(asset.Id, EffectAssetKind.Showcase)
        {
            AssetId = asset.Id;
            Path = asset.Path;
            _luaHost = luaHost;
            _project = project;
            if (IsUserScript)
            {
                Title = Loc.Text("Showcase.Showcases");
            }

            PreviewFrame = EffectPreviewFrameBuilder.BuildPlaceholder(EffectAssetKind.Showcase, asset.Id);
            TextureSource = new Rendering.TextureUpload.ProjectTextureSource(project);
            _scriptText = LoadScriptText(asset, project);
            BuildScriptTools(project);
            Loc.LanguageChanged += OnLanguageChanged;
            MarkClean();
        }

        public override void Dispose()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
            base.Dispose();
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

        public override IRenderFrameProvider CreatePreviewExportFrameProvider()
        {
            EffectWorld exportWorld = new();
            exportWorld.LoadProject(_project);
            LuaRunResult result = new LuaHost(exportWorld).Run(ScriptText);
            if (!result.Success || result.FrameProvider is null)
            {
                exportWorld.Dispose();
                return null;
            }

            if (result.FrameProvider is ShowcaseScene scene)
            {
                scene.MaxUpdateStepsPerFrame = TodLibConstants.TICKS_PER_SECOND * 2;
            }

            return new ExportShowcaseFrameProvider(exportWorld, result.FrameProvider);
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
                ? Loc.Format("Showcase.RanScript", result.SceneObjects.Count)
                : Loc.Text("Showcase.ScriptFailed");

            if (!Logs.Any())
            {
                AddRuntimeLog(Status);
            }

            if (!result.Success)
            {
                SelectedLog = Logs.FirstOrDefault(log => log.HasLocation) ?? Logs.FirstOrDefault();
            }
        }

        partial void OnSelectedLogChanged(ShowcaseLogEntry value)
        {
            OnPropertyChanged(nameof(SelectedLogDetail));
            if (value?.HasLocation == true)
            {
                ScriptNavigationRequested?.Invoke(
                    this,
                    new ShowcaseScriptNavigationRequest(value.LineNumber, value.ColumnNumber));
            }
        }

        [RelayCommand]
        private void ApplySelectedScriptTemplate()
        {
            if (SelectedScriptTemplate is null)
            {
                return;
            }

            ScriptEditRequested?.Invoke(this, new ShowcaseScriptEditRequest(SelectedScriptTemplate.Text, replaceDocument: true));
        }

        [RelayCommand]
        private void InsertSelectedScriptSnippet()
        {
            if (SelectedScriptSnippet is null)
            {
                return;
            }

            ScriptEditRequested?.Invoke(this, new ShowcaseScriptEditRequest(SelectedScriptSnippet.Text, replaceDocument: false));
        }

        [RelayCommand]
        private void InsertSelectedResourceId()
        {
            if (SelectedResourceReference is null)
            {
                return;
            }

            ScriptEditRequested?.Invoke(this, new ShowcaseScriptEditRequest(SelectedResourceReference.InsertText, replaceDocument: false));
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
            if (string.IsNullOrWhiteSpace(path) || path == UserScriptPath)
            {
                return string.Empty;
            }

            string normalizedPath = path.Replace('\\', System.IO.Path.DirectorySeparatorChar).Replace('/', System.IO.Path.DirectorySeparatorChar);
            return System.IO.Path.IsPathRooted(normalizedPath) || string.IsNullOrWhiteSpace(project?.RootPath)
                ? normalizedPath
                : System.IO.Path.Combine(project.RootPath, normalizedPath);
        }

        private static LocalizationManager Loc => LocalizationManager.Instance;
        private bool IsUserScript => Path == UserScriptPath;

        private void BuildScriptTools(EffectProject project)
        {
            foreach (ShowcaseScriptAction template in CreateTemplates(project))
            {
                ScriptTemplates.Add(template);
            }

            foreach (ShowcaseScriptAction snippet in CreateSnippets())
            {
                ScriptSnippets.Add(snippet);
            }

            foreach (ShowcaseResourceReference reference in CreateResourceReferences(project))
            {
                ResourceReferences.Add(reference);
            }

            SelectedScriptTemplate = ScriptTemplates.FirstOrDefault();
            SelectedScriptSnippet = ScriptSnippets.FirstOrDefault();
            SelectedResourceReference = ResourceReferences.FirstOrDefault();
        }

        private static IEnumerable<ShowcaseScriptAction> CreateTemplates(EffectProject project)
        {
            string reanimId = FirstId(project?.Manifest?.Reanims.Select(asset => asset.Id), "sample_reanim");
            string particleId = FirstId(project?.Manifest?.Particles.Select(asset => asset.Id), "sample_particle");
            string trailId = FirstId(project?.Manifest?.Trails.Select(asset => asset.Id), "sample_trail");
            yield return new ShowcaseScriptAction(
                "Scene loop",
                "A complete update/draw loop.",
                $$"""
                scene.clear()

                local body = effect.reanim("{{EscapeLuaString(reanimId)}}", 400, 300)
                local t = 0

                function update(dt)
                    t = t + dt
                    body:set_position(400 + math.sin(t * 2) * 40, 300)
                    body:update()
                end

                function draw(g)
                    body:draw(g)
                end

                effect.log("showcase initialized")
                """);

            yield return new ShowcaseScriptAction(
                "Reanim + particle + trail",
                "Compose the three main effect types.",
                $$"""
                scene.clear()

                local body = effect.reanim("{{EscapeLuaString(reanimId)}}", 400, 300)
                local burst = effect.particle("{{EscapeLuaString(particleId)}}", 420, 280)
                local slash = effect.trail("{{EscapeLuaString(trailId)}}", 0, 0)
                local t = 0

                function update(dt)
                    t = t + dt
                    body:set_position(400 + math.sin(t * 2) * 40, 300)
                    burst:set_scale(0.85 + math.sin(t * 3) * 0.15)
                    slash:clear_points()
                    slash:add_point(330, 320)
                    slash:add_point(470, 280 + math.sin(t * 4) * 40)
                end

                function draw(g)
                    body:draw(g)
                    burst:draw(g)
                    slash:draw(g)
                end
                """);
        }

        private static IEnumerable<ShowcaseScriptAction> CreateSnippets()
        {
            yield return new ShowcaseScriptAction(
                "update(dt)",
                "Per-frame update callback.",
                """
                function update(dt)
                    $0
                end
                """);
            yield return new ShowcaseScriptAction(
                "draw(g)",
                "Per-frame draw callback.",
                """
                function draw(g)
                    $0
                end
                """);
            yield return new ShowcaseScriptAction(
                "effect.reanim",
                "Create a reanimation object.",
                "local body = effect.reanim($0, 400, 300)");
            yield return new ShowcaseScriptAction(
                "effect.particle",
                "Create a particle object.",
                "local fx = effect.particle($0, 400, 300)");
            yield return new ShowcaseScriptAction(
                "effect.trail",
                "Create a trail object.",
                "local trail = effect.trail($0, 400, 300)");
            yield return new ShowcaseScriptAction(
                "trail points",
                "Reset and draw two manual trail points.",
                """
                trail:clear_points()
                trail:add_point($0, 300)
                trail:add_point(460, 300)
                """);
            yield return new ShowcaseScriptAction(
                "effect.log",
                "Write to the showcase log.",
                "effect.log($0)");
        }

        private static IEnumerable<ShowcaseResourceReference> CreateResourceReferences(EffectProject project)
        {
            if (project?.Manifest is null)
            {
                yield break;
            }

            foreach (string id in project.Manifest.Images.Select(asset => asset.Id).Where(IsNotBlank).OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
            {
                yield return new ShowcaseResourceReference(EffectAssetKind.Image, id);
            }

            foreach (string id in project.Manifest.Reanims.Select(asset => asset.Id).Where(IsNotBlank).OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
            {
                yield return new ShowcaseResourceReference(EffectAssetKind.Reanim, id);
            }

            foreach (string id in project.Manifest.Particles.Select(asset => asset.Id).Where(IsNotBlank).OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
            {
                yield return new ShowcaseResourceReference(EffectAssetKind.Particle, id);
            }

            foreach (string id in project.Manifest.Trails.Select(asset => asset.Id).Where(IsNotBlank).OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
            {
                yield return new ShowcaseResourceReference(EffectAssetKind.Trail, id);
            }

            foreach (string id in project.Manifest.Showcases.Select(asset => asset.Id).Where(IsNotBlank).OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
            {
                yield return new ShowcaseResourceReference(EffectAssetKind.Showcase, id);
            }
        }

        private static IEnumerable<ShowcaseCompletionItem> CreateCompletionItems()
        {
            yield return new ShowcaseCompletionItem("scene.clear()", "scene.clear()", "Clear the scene before composing objects.", isMember: false);
            yield return new ShowcaseCompletionItem("scene.count()", "scene.count()", "Return scene object count.", isMember: false);
            yield return new ShowcaseCompletionItem("scene.find_reanim(id)", "scene.find_reanim($0)", "Find a reanimation created in this scene.", isMember: false);
            yield return new ShowcaseCompletionItem("scene.find_particle(id)", "scene.find_particle($0)", "Find a particle created in this scene.", isMember: false);
            yield return new ShowcaseCompletionItem("scene.find_trail(id)", "scene.find_trail($0)", "Find a trail created in this scene.", isMember: false);
            yield return new ShowcaseCompletionItem("effect.reanim(id, x, y)", "effect.reanim($0, 400, 300)", "Create a reanimation.", isMember: false);
            yield return new ShowcaseCompletionItem("effect.particle(id, x, y)", "effect.particle($0, 400, 300)", "Create a particle effect.", isMember: false);
            yield return new ShowcaseCompletionItem("effect.trail(id, x, y)", "effect.trail($0, 400, 300)", "Create a trail effect.", isMember: false);
            yield return new ShowcaseCompletionItem("effect.image(id)", "effect.image($0)", "Load an image resource.", isMember: false);
            yield return new ShowcaseCompletionItem("effect.log(message)", "effect.log($0)", "Write a log entry.", isMember: false);
            yield return new ShowcaseCompletionItem("effect.warn(message)", "effect.warn($0)", "Write a warning log entry.", isMember: false);
            yield return new ShowcaseCompletionItem("effect.vector(x, y)", "effect.vector($0, 0)", "Create a vector.", isMember: false);
            yield return new ShowcaseCompletionItem("function update(dt)", "function update(dt)\n    $0\nend", "Define a per-frame update callback.", isMember: false);
            yield return new ShowcaseCompletionItem("function draw(g)", "function draw(g)\n    $0\nend", "Define a per-frame draw callback.", isMember: false);

            foreach ((string name, string description) in new[]
            {
                ("set_position(x, y)", "Move an object to an absolute position."),
                ("move(x, y)", "Move an object by an offset."),
                ("offset(x, y)", "Offset an object by an amount."),
                ("set_scale(scale)", "Set uniform scale."),
                ("set_color(r, g, b, a)", "Set object color."),
                ("set_image_override(imageId)", "Override the displayed image."),
                ("clear_image_override()", "Clear image override."),
                ("update()", "Advance this object."),
                ("draw(g)", "Draw this object."),
                ("die()", "Remove this object."),
                ("clear_points()", "Clear all trail points."),
                ("add_point(x, y)", "Add a trail point."),
                ("attach_reanim(trackName, child)", "Attach a reanimation to a track."),
                ("attach_particle(trackName, child)", "Attach a particle to a track."),
                ("attach_trail(trackName, child)", "Attach a trail to a track.")
            })
            {
                yield return new ShowcaseCompletionItem(name, name, description, isMember: true);
            }
        }

        private static bool IsNotBlank(string value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        private static string FirstId(IEnumerable<string> ids, string fallback)
        {
            return ids?
                .Where(IsNotBlank)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault()
                ?? fallback;
        }

        private static string EscapeLuaString(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            if (IsUserScript)
            {
                Title = Loc.Text("Showcase.Showcases");
            }

            OnPropertyChanged(nameof(DisplayAssetId));
            OnPropertyChanged(nameof(DisplayPath));
            OnPropertyChanged(nameof(SelectedLogDetail));
        }

        private sealed class ExportShowcaseFrameProvider : IRenderFrameProvider, IDisposable
        {
            private readonly EffectWorld _world;
            private readonly IRenderFrameProvider _provider;
            private bool _disposed;

            public ExportShowcaseFrameProvider(EffectWorld world, IRenderFrameProvider provider)
            {
                _world = world;
                _provider = provider;
            }

            public RenderFrame GetFrame(double deltaSeconds)
            {
                return _disposed ? new RenderFrame() : _provider.GetFrame(deltaSeconds);
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _world.Dispose();
            }
        }
    }
}
