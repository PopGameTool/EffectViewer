using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
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
        private readonly EffectProjectService _projectService;
        private readonly EffectWorld _effectWorld = new();
        private LuaHost _luaHost;
        private TaskCompletionSource<UnsavedChangesChoice> _unsavedChangesCompletion;

        public ObservableCollection<ProjectExplorerItemViewModel> ProjectItems { get; } = [];
        public ObservableCollection<EditorViewModelBase> OpenEditors { get; } = [];
        public ObservableCollection<ProjectListItemViewModel> AvailableProjects { get; } = [];

        [ObservableProperty]
        private EffectProject _currentProject;

        [ObservableProperty]
        private ProjectExplorerItemViewModel _selectedProjectItem;

        [ObservableProperty]
        private EditorViewModelBase _selectedEditor;

        [ObservableProperty]
        private string _statusText = "No project loaded";

        [ObservableProperty]
        private bool _isUnsavedChangesPromptOpen;

        [ObservableProperty]
        private string _unsavedChangesTitle;

        [ObservableProperty]
        private string _unsavedChangesMessage;

        [ObservableProperty]
        private bool _isOpenProjectDialogOpen;

        [ObservableProperty]
        private ProjectListItemViewModel _selectedAvailableProject;

        [ObservableProperty]
        private string _openProjectMessage;

        [ObservableProperty]
        private bool _isProjectTransferInProgress;

        [ObservableProperty]
        private string _projectTransferTitle;

        [ObservableProperty]
        private string _projectTransferMessage;

        [ObservableProperty]
        private double _projectTransferProgressValue;

        [ObservableProperty]
        private string _projectTransferProgressText;

        public bool HasAvailableProjects => AvailableProjects.Count > 0;

        private enum UnsavedChangesChoice
        {
            Save,
            Discard,
            Cancel
        }

        public MainViewModel(IProjectStorageProvider storageProvider)
        {
            _projectService = new EffectProjectService(storageProvider);
            LoadProject(_projectService.CreateDemoProject());
            StatusText = "Demo project loaded";
        }

        [RelayCommand]
        private async Task LoadDemoProjectAsync()
        {
            if (!await ConfirmAllUnsavedChangesAsync())
            {
                StatusText = "Canceled opening demo project.";
                return;
            }

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
            foreach (EditorViewModelBase editor in OpenEditors.Where(editor => editor.SavesWithProjectManifest))
            {
                editor.AcceptSavedState();
            }

            StatusText = $"Saved {CurrentProject.Manifest.Name}.";
        }

        [RelayCommand]
        private async Task SaveFileAsync()
        {
            if (SelectedEditor is null)
            {
                StatusText = "No document is selected.";
                return;
            }

            await SaveEditorAsync(SelectedEditor);
        }

        public async Task ImportResourceFolderAsync(string sourceDirectory)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory))
            {
                return;
            }

            if (!await ConfirmAllUnsavedChangesAsync())
            {
                StatusText = "Canceled folder import.";
                return;
            }

            FolderImportResult result = await _projectService.ImportFolderAsync(sourceDirectory);

            LoadProject(result.Project);
            StatusText = $"Imported {result.ImageCount} image(s), {result.ReanimCount} reanim(s), {result.ParticleCount} particle(s), {result.TrailCount} trail(s). Missing images: {result.MissingImageCount}.";
        }

        public async Task ImportProjectZipAsync(Stream zipStream)
        {
            if (zipStream is null)
            {
                return;
            }

            if (!await ConfirmAllUnsavedChangesAsync())
            {
                StatusText = "Canceled project import.";
                return;
            }

            try
            {
                BeginProjectTransfer("Importing Project", "Reading archive");
                Progress<ProjectTransferProgress> progress = new(UpdateProjectTransferProgress);
                EffectProject project = await _projectService.ImportProjectZipAsync(zipStream, progress);
                LoadProject(project);
                StatusText = $"Imported project {project.Manifest.Name}.";
            }
            catch (System.Exception ex) when (ex is IOException or System.IO.InvalidDataException or System.Text.Json.JsonException or System.InvalidOperationException)
            {
                StatusText = $"Could not import project: {ex.Message}";
            }
            finally
            {
                EndProjectTransfer();
            }
        }

        public async Task ExportCurrentProjectZipAsync(Stream outputStream)
        {
            if (outputStream is null)
            {
                return;
            }

            if (CurrentProject is null || string.IsNullOrWhiteSpace(CurrentProject.RootPath))
            {
                StatusText = "No internal project is loaded.";
                return;
            }

            foreach (EditorViewModelBase editor in OpenEditors.Where(editor => editor.IsDirty).ToList())
            {
                if (!await SaveEditorAsync(editor))
                {
                    StatusText = $"Canceled exporting {CurrentProject.Manifest.Name}.";
                    return;
                }
            }

            try
            {
                BeginProjectTransfer("Exporting Project", "Preparing archive");
                Progress<ProjectTransferProgress> progress = new(UpdateProjectTransferProgress);
                await _projectService.ExportProjectZipAsync(CurrentProject, outputStream, progress);
                StatusText = $"Exported {CurrentProject.Manifest.Name}.";
            }
            catch (System.Exception ex) when (ex is IOException or System.UnauthorizedAccessException or System.InvalidOperationException)
            {
                StatusText = $"Could not export project: {ex.Message}";
            }
            finally
            {
                EndProjectTransfer();
            }
        }

        private void BeginProjectTransfer(string title, string message)
        {
            ProjectTransferTitle = title;
            ProjectTransferMessage = message;
            ProjectTransferProgressValue = 0d;
            ProjectTransferProgressText = string.Empty;
            IsProjectTransferInProgress = true;
        }

        private void UpdateProjectTransferProgress(ProjectTransferProgress progress)
        {
            if (progress is null)
            {
                return;
            }

            ProjectTransferTitle = string.IsNullOrWhiteSpace(progress.Operation)
                ? ProjectTransferTitle
                : progress.Operation;
            ProjectTransferMessage = progress.Message;
            ProjectTransferProgressValue = progress.Ratio * 100d;
            ProjectTransferProgressText = progress.TotalItems <= 0
                ? string.Empty
                : $"{progress.CompletedItems} / {progress.TotalItems}";
        }

        private void EndProjectTransfer()
        {
            IsProjectTransferInProgress = false;
            ProjectTransferProgressValue = 0d;
            ProjectTransferProgressText = string.Empty;
        }

        [RelayCommand]
        private async Task ShowOpenProjectDialogAsync()
        {
            AvailableProjects.Clear();
            SelectedAvailableProject = null;

            IReadOnlyList<ProjectInfo> projects = await _projectService.ListProjectsAsync();
            foreach (ProjectInfo project in projects)
            {
                AvailableProjects.Add(new ProjectListItemViewModel(project));
            }

            OnPropertyChanged(nameof(HasAvailableProjects));
            OpenProjectMessage = HasAvailableProjects
                ? "Select a project from the app private project folder."
                : "No imported projects were found in the app private project folder.";
            IsOpenProjectDialogOpen = true;
            StatusText = HasAvailableProjects
                ? $"Found {AvailableProjects.Count} project(s)."
                : "No internal projects found.";
        }

        [RelayCommand]
        private async Task OpenSelectedProjectAsync()
        {
            ProjectListItemViewModel projectItem = SelectedAvailableProject;
            if (projectItem is null || string.IsNullOrWhiteSpace(projectItem.ProjectPath))
            {
                StatusText = "No project is selected.";
                return;
            }

            IsOpenProjectDialogOpen = false;
            if (!await ConfirmAllUnsavedChangesAsync())
            {
                StatusText = "Canceled opening project.";
                IsOpenProjectDialogOpen = true;
                return;
            }

            EffectProject project = await _projectService.LoadAsync(projectItem.ProjectPath);
            LoadProject(project);
            StatusText = $"Opened {project.Manifest.Name}.";
        }

        [RelayCommand]
        private void CancelOpenProject()
        {
            IsOpenProjectDialogOpen = false;
            SelectedAvailableProject = null;
            StatusText = "Canceled opening project.";
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
        private async Task CloseEditorAsync(EditorViewModelBase editor)
        {
            if (editor is null)
            {
                return;
            }

            if (!await ConfirmUnsavedChangesAsync(editor))
            {
                StatusText = $"Canceled closing {editor.Title}.";
                return;
            }

            CloseEditorCore(editor);
        }

        private void CloseEditorCore(EditorViewModelBase editor)
        {
            int index = OpenEditors.IndexOf(editor);
            bool wasSelected = ReferenceEquals(SelectedEditor, editor);
            editor.PropertyChanged -= OnOpenEditorPropertyChanged;
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
            editor.PropertyChanged += OnOpenEditorPropertyChanged;
            OpenEditors.Add(editor);
            SelectedEditor = editor;
            editor.IsSelected = ReferenceEquals(editor, SelectedEditor);
        }

        private void CloseAllEditors()
        {
            foreach (EditorViewModelBase editor in OpenEditors.ToList())
            {
                editor.PropertyChanged -= OnOpenEditorPropertyChanged;
                editor.Dispose();
            }

            OpenEditors.Clear();
            SelectedEditor = null;
        }

        public async Task<bool> ConfirmAllUnsavedChangesAsync()
        {
            foreach (EditorViewModelBase editor in OpenEditors.Where(editor => editor.IsDirty).ToList())
            {
                if (!await ConfirmUnsavedChangesAsync(editor))
                {
                    return false;
                }
            }

            return true;
        }

        private async Task<bool> ConfirmUnsavedChangesAsync(EditorViewModelBase editor)
        {
            if (editor is null || !editor.IsDirty)
            {
                return true;
            }

            SelectedEditor = editor;
            UnsavedChangesChoice choice = await PromptUnsavedChangesAsync(editor);
            switch (choice)
            {
                case UnsavedChangesChoice.Save:
                    if (!await SaveEditorAsync(editor))
                    {
                        return false;
                    }

                    return true;

                case UnsavedChangesChoice.Discard:
                    editor.DiscardChanges();
                    StatusText = $"Discarded changes to {editor.Title}.";
                    return true;

                default:
                    return false;
            }
        }

        private async Task<bool> SaveEditorAsync(EditorViewModelBase editor)
        {
            if (editor is null)
            {
                return false;
            }

            if (!editor.SupportsSave)
            {
                StatusText = $"{editor.Title} does not support file saving yet.";
                return false;
            }

            if (CurrentProject is null || string.IsNullOrWhiteSpace(CurrentProject.RootPath))
            {
                StatusText = "No writable project is loaded.";
                return false;
            }

            try
            {
                await editor.SaveAsync(_projectService, CurrentProject);
                StatusText = $"Saved {editor.Title}.";
                return true;
            }
            catch (System.Exception ex) when (ex is IOException or System.FormatException or System.InvalidOperationException)
            {
                StatusText = $"Could not save {editor.Title}: {ex.Message}";
                return false;
            }
        }

        private Task<UnsavedChangesChoice> PromptUnsavedChangesAsync(EditorViewModelBase editor)
        {
            _unsavedChangesCompletion = new TaskCompletionSource<UnsavedChangesChoice>();
            UnsavedChangesTitle = "Unsaved Changes";
            UnsavedChangesMessage = $"{editor.Title} has unsaved changes.";
            IsUnsavedChangesPromptOpen = true;
            return _unsavedChangesCompletion.Task;
        }

        [RelayCommand]
        private void SaveUnsavedChanges()
        {
            CompleteUnsavedChangesPrompt(UnsavedChangesChoice.Save);
        }

        [RelayCommand]
        private void DiscardUnsavedChanges()
        {
            CompleteUnsavedChangesPrompt(UnsavedChangesChoice.Discard);
        }

        [RelayCommand]
        private void CancelUnsavedChanges()
        {
            CompleteUnsavedChangesPrompt(UnsavedChangesChoice.Cancel);
        }

        private void CompleteUnsavedChangesPrompt(UnsavedChangesChoice choice)
        {
            IsUnsavedChangesPromptOpen = false;
            _unsavedChangesCompletion?.TrySetResult(choice);
            _unsavedChangesCompletion = null;
        }

        partial void OnSelectedEditorChanged(EditorViewModelBase value)
        {
            foreach (EditorViewModelBase editor in OpenEditors)
            {
                editor.IsSelected = ReferenceEquals(editor, value);
            }

            if (value is not null)
            {
                StatusText = value.IsDirty
                    ? $"Editing {value.Title} (unsaved)."
                    : $"Editing {value.Title}.";
            }
        }

        private void OnOpenEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is not EditorViewModelBase editor || !ReferenceEquals(editor, SelectedEditor))
            {
                return;
            }

            if (e.PropertyName == nameof(EditorViewModelBase.IsDirty))
            {
                StatusText = editor.IsDirty
                    ? $"Editing {editor.Title} (unsaved)."
                    : $"Editing {editor.Title}.";
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
