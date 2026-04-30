using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EffectViewer.Projects;
using EffectViewer.Runtime;
using EffectViewer.Runtime.Lua;

namespace EffectViewer.ViewModels
{
    public sealed partial class MainViewModel : ViewModelBase
    {
        private readonly EffectProjectService _projectService = new();
        private readonly EffectWorld _effectWorld = new();
        private LuaHost _luaHost;

        public ObservableCollection<ProjectExplorerItemViewModel> ProjectItems { get; } = [];
        public ObservableCollection<EditorViewModelBase> OpenEditors { get; } = [];

        [ObservableProperty]
        private EffectProject _currentProject;

        [ObservableProperty]
        private ProjectExplorerItemViewModel _selectedProjectItem;

        [ObservableProperty]
        private EditorViewModelBase _selectedEditor;

        [ObservableProperty]
        private string _statusText = "No project loaded";

        public MainViewModel()
        {
            LoadDemoProject();
        }

        [RelayCommand]
        private void LoadDemoProject()
        {
            LoadProject(_projectService.CreateDemoProject());
            StatusText = "Demo project loaded";
        }

        [RelayCommand]
        private async Task SaveProjectAsync()
        {
            if (CurrentProject is null || string.IsNullOrWhiteSpace(CurrentProject.RootPath))
            {
                StatusText = "No writable project is loaded.";
                return;
            }

            await _projectService.SaveAsync(CurrentProject);
            StatusText = $"Saved {CurrentProject.Manifest.Name}.";
        }

        public async Task ImportResourceFolderAsync(string sourceDirectory)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory))
            {
                return;
            }

            string projectDirectory = sourceDirectory;
            FolderImportResult result = await _projectService.ImportFolderAsync(
                sourceDirectory,
                projectDirectory,
                ImportMode.ReferenceSource);

            LoadProject(result.Project);
            StatusText = $"Imported {result.ImageCount} image(s), {result.ReanimCount} reanim(s), {result.ParticleCount} particle(s), {result.TrailCount} trail(s). Missing images: {result.MissingImageCount}.";
        }

        public async Task OpenProjectAsync(string projectDirectory)
        {
            if (string.IsNullOrWhiteSpace(projectDirectory))
            {
                return;
            }

            EffectProject project = await _projectService.LoadAsync(projectDirectory);
            LoadProject(project);
            StatusText = $"Opened {project.Manifest.Name}.";
        }

        private void LoadProject(EffectProject project)
        {
            CurrentProject = project;
            _effectWorld.LoadProject(CurrentProject);
            _luaHost = new LuaHost(_effectWorld);
            SelectedProjectItem = null;
            RebuildProjectTree();
            CloseAllEditors();
            OpenEditorTab(new WelcomeEditorViewModel());
        }

        partial void OnSelectedProjectItemChanged(ProjectExplorerItemViewModel value)
        {
            if (value is null || !value.IsSelectable)
            {
                return;
            }

            OpenEditor(value);
        }

        private void OpenEditor(ProjectExplorerItemViewModel item)
        {
            switch (item.Kind)
            {
                case EffectAssetKind.Image:
                    if (CurrentProject.Assets.Images.TryGetValue(item.AssetId, out Assets.ImageAsset image))
                    {
                        OpenOrSelectEditor(item.Kind, item.AssetId, () => new ImageEditorViewModel(image, CurrentProject));
                        StatusText = $"Editing image {image.Id}";
                    }
                    break;

                case EffectAssetKind.Reanim:
                    OpenOrSelectEditor(item.Kind, item.AssetId, () => new EffectEditorViewModel(item.Kind, item.AssetId, item.Path, CurrentProject));
                    StatusText = $"Editing reanim {item.AssetId}";
                    break;

                case EffectAssetKind.Particle:
                    OpenOrSelectEditor(item.Kind, item.AssetId, () => new EffectEditorViewModel(item.Kind, item.AssetId, item.Path, CurrentProject));
                    StatusText = $"Editing particle {item.AssetId}";
                    break;

                case EffectAssetKind.Trail:
                    OpenOrSelectEditor(item.Kind, item.AssetId, () => new EffectEditorViewModel(item.Kind, item.AssetId, item.Path, CurrentProject));
                    StatusText = $"Editing trail {item.AssetId}";
                    break;

                case EffectAssetKind.Showcase:
                    if (CurrentProject.Assets.Showcases.TryGetValue(item.AssetId, out ShowcaseAsset showcase))
                    {
                        OpenOrSelectEditor(item.Kind, item.AssetId, () => new ShowcaseEditorViewModel(showcase, _luaHost, CurrentProject));
                        StatusText = $"Editing showcase {item.AssetId}";
                    }
                    else
                    {
                        OpenOrSelectEditor(item.Kind, item.AssetId, () => new ShowcaseEditorViewModel(_luaHost, CurrentProject));
                        StatusText = "Editing showcases";
                    }
                    break;
            }
        }

        [RelayCommand]
        private void SelectEditor(EditorViewModelBase editor)
        {
            if (editor is not null)
            {
                SelectedEditor = editor;
            }
        }

        [RelayCommand]
        private void CloseEditor(EditorViewModelBase editor)
        {
            if (editor is null)
            {
                return;
            }

            int index = OpenEditors.IndexOf(editor);
            bool wasSelected = ReferenceEquals(SelectedEditor, editor);
            OpenEditors.Remove(editor);
            editor.Dispose();
            SelectedProjectItem = null;

            if (wasSelected)
            {
                SelectedEditor = OpenEditors.Count == 0
                    ? null
                    : OpenEditors[System.Math.Clamp(index, 0, OpenEditors.Count - 1)];
            }
        }

        private void OpenOrSelectEditor(EffectAssetKind kind, string title, System.Func<EditorViewModelBase> editorFactory)
        {
            string documentId = EditorViewModelBase.CreateDocumentId(kind, title);
            EditorViewModelBase existing = OpenEditors.FirstOrDefault(editor => editor.DocumentId == documentId);
            if (existing is not null)
            {
                SelectedEditor = existing;
                return;
            }

            OpenEditorTab(editorFactory());
        }

        private void OpenEditorTab(EditorViewModelBase editor)
        {
            OpenEditors.Add(editor);
            SelectedEditor = editor;
            editor.IsSelected = ReferenceEquals(editor, SelectedEditor);
        }

        private void CloseAllEditors()
        {
            foreach (EditorViewModelBase editor in OpenEditors.ToList())
            {
                editor.Dispose();
            }

            OpenEditors.Clear();
            SelectedEditor = null;
        }

        partial void OnSelectedEditorChanged(EditorViewModelBase value)
        {
            foreach (EditorViewModelBase editor in OpenEditors)
            {
                editor.IsSelected = ReferenceEquals(editor, value);
            }
        }

        private void RebuildProjectTree()
        {
            ProjectItems.Clear();

            ProjectExplorerItemViewModel root = new(CurrentProject.Manifest.Name, EffectAssetKind.Project)
            {
                IsExpanded = true
            };

            root.Children.Add(CreateFolder("Images", CurrentProject.Manifest.Images.Select(asset =>
                new ProjectExplorerItemViewModel(asset.Id, EffectAssetKind.Image, asset.Id, asset.Path))));

            root.Children.Add(CreateFolder("Reanim", CurrentProject.Manifest.Reanims.Select(asset =>
                new ProjectExplorerItemViewModel(asset.Id, EffectAssetKind.Reanim, asset.Id, asset.Path))));

            root.Children.Add(CreateFolder("Particles", CurrentProject.Manifest.Particles.Select(asset =>
                new ProjectExplorerItemViewModel(asset.Id, EffectAssetKind.Particle, asset.Id, asset.Path))));

            root.Children.Add(CreateFolder("Trails", CurrentProject.Manifest.Trails.Select(asset =>
                new ProjectExplorerItemViewModel(asset.Id, EffectAssetKind.Trail, asset.Id, asset.Path))));

            ProjectExplorerItemViewModel showcases = new("Showcases", EffectAssetKind.Showcase, "Showcases")
            {
                IsExpanded = true
            };
            foreach (ShowcaseAsset asset in CurrentProject.Manifest.Showcases)
            {
                showcases.Children.Add(new ProjectExplorerItemViewModel(asset.Id, EffectAssetKind.Showcase, asset.Id, asset.Path));
            }

            root.Children.Add(showcases);

            ProjectItems.Add(root);
        }

        private static ProjectExplorerItemViewModel CreateFolder(
            string title,
            IEnumerable<ProjectExplorerItemViewModel> children)
        {
            ProjectExplorerItemViewModel folder = new(title, EffectAssetKind.Folder)
            {
                IsExpanded = true
            };

            foreach (ProjectExplorerItemViewModel child in children)
            {
                folder.Children.Add(child);
            }

            return folder;
        }
    }
}
