using System.Collections.ObjectModel;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private bool _canUndoScriptEdit;
        private bool _canRedoScriptEdit;
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
        public override bool CanUndo => _canUndoScriptEdit;
        public override bool CanRedo => _canRedoScriptEdit;

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
        public event EventHandler ScriptUndoRequested;
        public event EventHandler ScriptRedoRequested;

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

        public override void Undo()
        {
            if (CanUndo)
            {
                ScriptUndoRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        public override void Redo()
        {
            if (CanRedo)
            {
                ScriptRedoRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        public void SetScriptUndoRedoState(bool canUndo, bool canRedo)
        {
            SetProperty(ref _canUndoScriptEdit, canUndo, nameof(CanUndo));
            SetProperty(ref _canRedoScriptEdit, canRedo, nameof(CanRedo));
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

                local body = scene.reanim("sample_reanim", 400, 300)
                local fire = scene.particle_system("fire_burst", 420, 280)
                local slash = scene.trail("sword_slash", 0, 0)

                local context = {}
                local t = 0

                function context:update(dt)
                    t = t + dt
                    body:set_position(400 + math.sin(t * 2) * 40, 300)
                    fire:set_scale(0.8 + math.sin(t * 3) * 0.2)
                    slash:clear_points()
                    slash:add_point(330, 320)
                    slash:add_point(470, 280 + math.sin(t * 4) * 40)
                end

                function context:draw(g)
                    body:draw(g)
                    fire:draw(g)
                    slash:draw(g)
                end

                scene.regist(context)
                scene.log("showcase initialized")
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

                local body = scene.reanim("{{EscapeLuaString(reanimId)}}", 400, 300)
                local context = {}
                local t = 0

                function context:update(dt)
                    t = t + dt
                    body:set_position(400 + math.sin(t * 2) * 40, 300)
                    body:update()
                end

                function context:draw(g)
                    body:draw(g)
                end

                scene.regist(context)
                scene.log("showcase initialized")
                """);

            yield return new ShowcaseScriptAction(
                "Reanim + particle + trail",
                "Compose the three main effect types.",
                $$"""
                scene.clear()

                local body = scene.reanim("{{EscapeLuaString(reanimId)}}", 400, 300)
                local burst = scene.particle_system("{{EscapeLuaString(particleId)}}", 420, 280)
                local slash = scene.trail("{{EscapeLuaString(trailId)}}", 0, 0)
                local context = {}
                local t = 0

                function context:update(dt)
                    t = t + dt
                    body:set_position(400 + math.sin(t * 2) * 40, 300)
                    burst:set_scale(0.85 + math.sin(t * 3) * 0.15)
                    slash:clear_points()
                    slash:add_point(330, 320)
                    slash:add_point(470, 280 + math.sin(t * 4) * 40)
                end

                function context:draw(g)
                    body:draw(g)
                    burst:draw(g)
                    slash:draw(g)
                end

                scene.regist(context)
                """);
        }

        private static IEnumerable<ShowcaseScriptAction> CreateSnippets()
        {
            yield return new ShowcaseScriptAction(
                "context:update(dt)",
                "Per-frame update callback.",
                """
                function context:update(dt)
                    $0
                end
                """);
            yield return new ShowcaseScriptAction(
                "context:draw(g)",
                "Per-frame draw callback.",
                """
                function context:draw(g)
                    $0
                end
                """);
            yield return new ShowcaseScriptAction(
                "scene.reanim",
                "Create a reanimation object.",
                "local body = scene.reanim($0, 400, 300)");
            yield return new ShowcaseScriptAction(
                "scene.particle_system",
                "Create a particle object.",
                "local fx = scene.particle_system($0, 400, 300)");
            yield return new ShowcaseScriptAction(
                "scene.trail",
                "Create a trail object.",
                "local trail = scene.trail($0, 400, 300)");
            yield return new ShowcaseScriptAction(
                "trail points",
                "Reset and draw two manual trail points.",
                """
                trail:clear_points()
                trail:add_point($0, 300)
                trail:add_point(460, 300)
                """);
            yield return new ShowcaseScriptAction(
                "scene.log",
                "Write to the showcase log.",
                "scene.log($0)");
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

            foreach (string id in project.Manifest.Fonts.Select(asset => asset.Id).Where(IsNotBlank).OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
            {
                yield return new ShowcaseResourceReference(EffectAssetKind.Font, id);
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
            yield return new ShowcaseCompletionItem("scene", "scene", "Showcase scene API.", ShowcaseCompletionScope.Global);
            yield return new ShowcaseCompletionItem("global_attachment", "global_attachment", "Global attachment API.", ShowcaseCompletionScope.Global);
            yield return new ShowcaseCompletionItem("function context:update(dt)", "function context:update(dt)\n    $0\nend", "Define a per-frame update callback.", ShowcaseCompletionScope.Global);
            yield return new ShowcaseCompletionItem("function context:draw(g)", "function context:draw(g)\n    $0\nend", "Define a per-frame draw callback.", ShowcaseCompletionScope.Global);

            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems(typeof(LuaSceneApi), ShowcaseCompletionScope.Scene, "Scene API"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateGlobalTypeCompletionItems("scene", typeof(LuaSceneApi), "Scene API"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems(typeof(LuaGraphicsApi), ShowcaseCompletionScope.Graphics, "Graphics API"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems(typeof(LuaAttachmentApi), ShowcaseCompletionScope.AttachmentApi, "Attachment API"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateGlobalTypeCompletionItems("global_attachment", typeof(LuaAttachmentApi), "Attachment API"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems<SceneObject>(ShowcaseCompletionScope.SceneObject, "Scene object"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems<ShowcaseReanimation>(ShowcaseCompletionScope.Reanimation, "Reanimation"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems<ShowcaseReanimationTrack>(ShowcaseCompletionScope.ReanimationTrack, "Reanimation track"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems<ShowcaseReanimationTransform>(ShowcaseCompletionScope.ReanimationTransform, "Reanimation transform"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems<ShowcaseReanimationFrameRange>(ShowcaseCompletionScope.ReanimationFrameRange, "Reanimation frame range"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems<ShowcaseFrameTime>(ShowcaseCompletionScope.FrameTime, "Frame time"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems<ShowcaseParticle>(ShowcaseCompletionScope.Particle, "Particle system"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems<ShowcaseParticleEmitter>(ShowcaseCompletionScope.ParticleEmitter, "Particle emitter"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems<ShowcaseParticleInstance>(ShowcaseCompletionScope.ParticleInstance, "Particle instance"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems<ShowcaseParticleRenderParams>(ShowcaseCompletionScope.ParticleRenderParams, "Particle render params"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems<ShowcaseTrail>(ShowcaseCompletionScope.Trail, "Trail"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems<ShowcaseTrailPoint>(ShowcaseCompletionScope.TrailPoint, "Trail point"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems<ShowcaseAttachment>(ShowcaseCompletionScope.Attachment, "Attachment"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems<ShowcaseAttachmentEffect>(ShowcaseCompletionScope.AttachmentEffect, "Attachment effect"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems<ShowcaseFont>(ShowcaseCompletionScope.Font, "Font"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems<ShowcaseImage>(ShowcaseCompletionScope.Image, "Image"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems<ShowcaseMatrix>(ShowcaseCompletionScope.Matrix, "Matrix"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems<ShowcaseVector>(ShowcaseCompletionScope.Vector, "Vector"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems<ShowcaseVector3>(ShowcaseCompletionScope.Vector3, "Vector3"))
            {
                yield return item;
            }

            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems<ShowcaseTriVertex>(ShowcaseCompletionScope.TriVertex, "Triangle vertex"))
            {
                yield return item;
            }
        }

        private static IEnumerable<ShowcaseCompletionItem> CreateGlobalTypeCompletionItems(
            string receiver,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicMethods)]
            Type type,
            string label)
        {
            foreach (ShowcaseCompletionItem item in CreateTypeCompletionItems(type, ShowcaseCompletionScope.Global, label, receiver))
            {
                yield return item;
            }
        }

        private static IEnumerable<ShowcaseCompletionItem> CreateTypeCompletionItems(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicMethods)]
            Type type,
            ShowcaseCompletionScope scope,
            string label,
            string receiver = null)
        {
            string prefix = string.IsNullOrWhiteSpace(receiver) ? string.Empty : $"{receiver}.";

            foreach (PropertyInfo property in type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(property => property.GetIndexParameters().Length == 0)
                .OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                string name = $"{prefix}{property.Name}";
                string mode = property.CanWrite ? "property" : "read-only property";
                yield return new ShowcaseCompletionItem(name, name, $"{label} {mode}.", scope);
            }

            foreach (MethodInfo method in type
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .OrderBy(method => method.Name, StringComparer.Ordinal)
                .ThenBy(method => method.GetParameters().Length))
            {
                string display = $"{prefix}{FormatMethodSignature(method)}";
                string insert = $"{prefix}{method.Name}({(method.GetParameters().Length == 0 ? string.Empty : "$0")})";
                yield return new ShowcaseCompletionItem(display, insert, $"{label} method.", scope);
            }
        }

        private static IEnumerable<ShowcaseCompletionItem> CreateTypeCompletionItems<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicMethods)]
            T>(
            ShowcaseCompletionScope scope,
            string label)
        {
            return CreateTypeCompletionItems(typeof(T), scope, label);
        }

        private static string FormatMethodSignature(MethodInfo method)
        {
            string parameters = string.Join(", ", method.GetParameters().Select(FormatParameterName));
            return $"{method.Name}({parameters})";
        }

        private static string FormatParameterName(ParameterInfo parameter)
        {
            string name = string.IsNullOrWhiteSpace(parameter.Name) ? "value" : parameter.Name;
            return parameter.GetCustomAttribute<ParamArrayAttribute>() is null ? name : $"{name}...";
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
