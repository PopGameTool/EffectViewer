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

        [ObservableProperty]
        private EffectProject _currentProject;

        [ObservableProperty]
        private ProjectExplorerItemViewModel _selectedProjectItem;

        [ObservableProperty]
        private EditorViewModelBase _currentEditor = new WelcomeEditorViewModel();

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
            RebuildProjectTree();
            SetCurrentEditor(new WelcomeEditorViewModel());
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
                        SetCurrentEditor(new ImageEditorViewModel(image, CurrentProject));
                        StatusText = $"Editing image {image.Id}";
                    }
                    break;

                case EffectAssetKind.Reanim:
                    SetCurrentEditor(new EffectEditorViewModel(item.Kind, item.AssetId, item.Path, CurrentProject));
                    StatusText = $"Editing reanim {item.AssetId}";
                    break;

                case EffectAssetKind.Particle:
                    SetCurrentEditor(new EffectEditorViewModel(item.Kind, item.AssetId, item.Path, CurrentProject));
                    StatusText = $"Editing particle {item.AssetId}";
                    break;

                case EffectAssetKind.Trail:
                    SetCurrentEditor(new EffectEditorViewModel(item.Kind, item.AssetId, item.Path, CurrentProject));
                    StatusText = $"Editing trail {item.AssetId}";
                    break;

                case EffectAssetKind.Showcase:
                    if (CurrentProject.Assets.Showcases.TryGetValue(item.AssetId, out ShowcaseAsset showcase))
                    {
                        SetCurrentEditor(new ShowcaseEditorViewModel(showcase, _luaHost));
                        StatusText = $"Editing showcase {item.AssetId}";
                    }
                    break;
            }
        }

        private void SetCurrentEditor(EditorViewModelBase editor)
        {
            if (!ReferenceEquals(CurrentEditor, editor))
            {
                CurrentEditor?.Dispose();
            }

            CurrentEditor = editor;
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

            root.Children.Add(CreateFolder("Showcases", CurrentProject.Manifest.Showcases.Select(asset =>
                new ProjectExplorerItemViewModel(asset.Id, EffectAssetKind.Showcase, asset.Id, asset.Path))));

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
