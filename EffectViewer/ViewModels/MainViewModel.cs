using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EffectViewer.Assets;
using EffectViewer.Localization;
using EffectViewer.Projects;
using EffectViewer.Runtime;
using EffectViewer.Runtime.Lua;

namespace EffectViewer.ViewModels
{
    public enum ProjectExplorerDockSide
    {
        Left,
        Right
    }

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
        private string _statusText = LocalizationManager.Instance.Text("Status.NoProjectLoaded");

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
        private bool _isProjectTransferProgressIndeterminate;

        [ObservableProperty]
        private string _projectTransferProgressText;

        [ObservableProperty]
        private ProjectExplorerDockSide _projectExplorerDockSide = ProjectExplorerDockSide.Left;

        [ObservableProperty]
        private bool _isProjectExplorerVisible = true;

        [ObservableProperty]
        private int _layoutResetRevision;

        [ObservableProperty]
        private bool _isNewProjectDialogOpen;

        [ObservableProperty]
        private string _newProjectName = LocalizationManager.Instance.Text("Dialog.UntitledEffectProject");

        [ObservableProperty]
        private bool _isNewResourceDialogOpen;

        [ObservableProperty]
        private EffectAssetKind _newResourceKind = EffectAssetKind.Reanim;

        [ObservableProperty]
        private string _newResourceId = "new_reanim";

        public IReadOnlyList<EffectAssetKind> NewResourceKinds { get; } =
        [
            EffectAssetKind.Reanim,
            EffectAssetKind.Particle,
            EffectAssetKind.Trail,
            EffectAssetKind.Showcase
        ];

        public bool IsProjectExplorerDockedLeft => ProjectExplorerDockSide == ProjectExplorerDockSide.Left;
        public bool IsProjectExplorerDockedRight => ProjectExplorerDockSide == ProjectExplorerDockSide.Right;
        public bool IsProjectExplorerVisibleLeft => IsProjectExplorerVisible && IsProjectExplorerDockedLeft;
        public bool IsProjectExplorerVisibleRight => IsProjectExplorerVisible && IsProjectExplorerDockedRight;
        public bool HasAvailableProjects => AvailableProjects.Count > 0;
        public bool CanSaveCurrentProject => CurrentProject is not null && !string.IsNullOrWhiteSpace(CurrentProject.RootPath);
        public bool CanSaveSelectedFile => CanSaveCurrentProject && SelectedEditor?.SupportsSave == true;
        public bool CanExportSelectedFile => CanSaveCurrentProject && SelectedEditor?.SupportsFileExport == true;
        public bool CanModifyCurrentProject => CanSaveCurrentProject;
        public bool IsEnglishLanguage => Loc.IsEnglish;
        public bool IsChineseLanguage => Loc.IsChinese;

        private enum UnsavedChangesChoice
        {
            Save,
            Discard,
            Cancel
        }

        public MainViewModel(IProjectStorageProvider storageProvider)
        {
            _projectService = new EffectProjectService(storageProvider);
            Loc.LanguageChanged += OnLanguageChanged;
            LoadProject(_projectService.CreateDemoProject());
            StatusText = T("Status.DemoProjectLoaded");
        }

        private static LocalizationManager Loc => LocalizationManager.Instance;
        private static string T(string key) => Loc.Text(key);
        private static string F(string key, params object[] args) => Loc.Format(key, args);

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(IsEnglishLanguage));
            OnPropertyChanged(nameof(IsChineseLanguage));

            if (CurrentProject is not null)
            {
                RebuildProjectTree();
            }

            if (IsUnsavedChangesPromptOpen && SelectedEditor is not null)
            {
                UnsavedChangesTitle = T("Dialog.UnsavedChangesTitle");
                UnsavedChangesMessage = F("Dialog.UnsavedChangesMessage", SelectedEditor.Title);
            }
        }

        [RelayCommand]
        private void SetEnglishLanguage()
        {
            SetLanguage(LocalizationManager.EnglishLanguageCode);
        }

        [RelayCommand]
        private void SetChineseLanguage()
        {
            SetLanguage(LocalizationManager.ChineseLanguageCode);
        }

        private void SetLanguage(string languageCode)
        {
            Loc.UseBuiltInLanguage(languageCode);
            string languageName = languageCode == LocalizationManager.ChineseLanguageCode
                ? T("Language.ChineseSimplified")
                : T("Language.English");
            StatusText = F("Status.LanguageChanged", languageName);
        }

        public async Task LoadLanguageFileAsync(Stream stream, string fileName)
        {
            try
            {
                await Loc.LoadLanguageFileAsync(stream, Path.GetFileNameWithoutExtension(fileName));
                StatusText = F("Status.LanguageFileLoaded", fileName);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
            {
                StatusText = F("Status.CouldNotLoadLanguageFile", ex.Message);
            }
        }

        partial void OnProjectExplorerDockSideChanged(ProjectExplorerDockSide value)
        {
            NotifyProjectExplorerLayoutProperties();
        }

        partial void OnIsProjectExplorerVisibleChanged(bool value)
        {
            NotifyProjectExplorerLayoutProperties();
        }

        private void NotifyProjectExplorerLayoutProperties()
        {
            OnPropertyChanged(nameof(IsProjectExplorerDockedLeft));
            OnPropertyChanged(nameof(IsProjectExplorerDockedRight));
            OnPropertyChanged(nameof(IsProjectExplorerVisibleLeft));
            OnPropertyChanged(nameof(IsProjectExplorerVisibleRight));
        }

        [RelayCommand]
        private void DockProjectExplorerLeft()
        {
            ProjectExplorerDockSide = ProjectExplorerDockSide.Left;
            IsProjectExplorerVisible = true;
            StatusText = T("Status.ProjectExplorerDockedLeft");
        }

        [RelayCommand]
        private void DockProjectExplorerRight()
        {
            ProjectExplorerDockSide = ProjectExplorerDockSide.Right;
            IsProjectExplorerVisible = true;
            StatusText = T("Status.ProjectExplorerDockedRight");
        }

        [RelayCommand]
        private void ToggleProjectExplorer()
        {
            IsProjectExplorerVisible = !IsProjectExplorerVisible;
            StatusText = IsProjectExplorerVisible
                ? T("Status.ProjectExplorerShown")
                : T("Status.ProjectExplorerHidden");
        }

        [RelayCommand]
        private void ResetLayout()
        {
            ProjectExplorerDockSide = ProjectExplorerDockSide.Left;
            IsProjectExplorerVisible = true;
            LayoutResetRevision++;
            StatusText = T("Status.LayoutReset");
        }

        partial void OnCurrentProjectChanged(EffectProject value)
        {
            OnPropertyChanged(nameof(CanSaveCurrentProject));
            OnPropertyChanged(nameof(CanSaveSelectedFile));
            OnPropertyChanged(nameof(CanExportSelectedFile));
            OnPropertyChanged(nameof(CanModifyCurrentProject));
        }

        partial void OnNewResourceKindChanged(EffectAssetKind value)
        {
            if (string.IsNullOrWhiteSpace(NewResourceId) || NewResourceId == "new_reanim" ||
                NewResourceId == "new_particle" || NewResourceId == "new_trail" ||
                NewResourceId == "new_showcase")
            {
                NewResourceId = value switch
                {
                    EffectAssetKind.Particle => "new_particle",
                    EffectAssetKind.Trail => "new_trail",
                    EffectAssetKind.Showcase => "new_showcase",
                    _ => "new_reanim"
                };
            }
        }

        [RelayCommand]
        private async Task LoadDemoProjectAsync()
        {
            if (!await ConfirmAllUnsavedChangesAsync())
            {
                StatusText = T("Status.CanceledOpeningDemoProject");
                return;
            }

            LoadProject(_projectService.CreateDemoProject());
            StatusText = T("Status.DemoProjectLoaded");
        }

        [RelayCommand]
        private void ShowNewProjectDialog()
        {
            NewProjectName = T("Dialog.UntitledEffectProject");
            IsNewProjectDialogOpen = true;
            StatusText = T("Status.CreatingNewProject");
        }

        [RelayCommand]
        private async Task CreateProjectAsync()
        {
            string projectName = string.IsNullOrWhiteSpace(NewProjectName)
                ? T("Dialog.UntitledEffectProject")
                : NewProjectName.Trim();

            IsNewProjectDialogOpen = false;
            if (!await ConfirmAllUnsavedChangesAsync())
            {
                StatusText = T("Status.CanceledCreatingProject");
                IsNewProjectDialogOpen = true;
                return;
            }

            try
            {
                EffectProject project = await _projectService.CreateProjectAsync(projectName);
                LoadProject(project);
                StatusText = F("Status.CreatedProject", project.Manifest.Name);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                StatusText = F("Status.CouldNotCreateProject", ex.Message);
            }
        }

        [RelayCommand]
        private void CancelNewProject()
        {
            IsNewProjectDialogOpen = false;
            StatusText = T("Status.CanceledCreatingProject");
        }

        [RelayCommand]
        private async Task SaveProjectAsync()
        {
            if (CurrentProject is null || string.IsNullOrWhiteSpace(CurrentProject.RootPath))
            {
                StatusText = T("Status.NoWritableProjectLoaded");
                return;
            }

            foreach (ImageEditorViewModel imageEditor in OpenEditors.OfType<ImageEditorViewModel>())
            {
                if (!imageEditor.ApplyPendingAssetId())
                {
                    StatusText = F("Status.CouldNotSaveImageIdEmpty", imageEditor.Title);
                    return;
                }
            }

            foreach (EffectEditorViewModel effectEditor in OpenEditors.OfType<EffectEditorViewModel>())
            {
                if (string.IsNullOrWhiteSpace(effectEditor.AssetId))
                {
                    StatusText = F("Status.CouldNotSaveAssetIdEmpty", effectEditor.Title);
                    return;
                }
            }

            await _projectService.SaveAsync(CurrentProject);
            foreach (EditorViewModelBase editor in OpenEditors.Where(editor => editor.SavesWithProjectManifest))
            {
                editor.AcceptSavedState();
            }

            StatusText = F("Status.Saved", CurrentProject.Manifest.Name);
        }

        [RelayCommand]
        private async Task SaveFileAsync()
        {
            if (SelectedEditor is null)
            {
                StatusText = T("Status.NoDocumentSelected");
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
                StatusText = T("Status.CanceledFolderImport");
                return;
            }

            try
            {
                BeginProjectTransfer(T("Transfer.ImportingResourceFolder"), T("Transfer.ScanningResourceFolder"));
                Progress<ProjectTransferProgress> progress = new(UpdateProjectTransferProgress);
                FolderImportResult result = await _projectService.ImportFolderAsync(sourceDirectory, progress);

                LoadProject(result.Project);
                StatusText = F("Status.ImportedFolder", result.ImageCount, result.ReanimCount, result.ParticleCount, result.TrailCount, result.MissingImageCount);
            }
            catch (Exception ex)
            {
                StatusText = F("Status.CouldNotImportFolder", ex.Message);
            }
            finally
            {
                EndProjectTransfer();
            }
        }

        public async Task ImportResourceFolderAsync(IStorageFolder sourceFolder)
        {
            if (sourceFolder is null)
            {
                return;
            }

            if (!await ConfirmAllUnsavedChangesAsync())
            {
                StatusText = T("Status.CanceledFolderImport");
                return;
            }

            try
            {
                BeginProjectTransfer(T("Transfer.ImportingResourceFolder"), T("Transfer.ReadingFolder"));
                Progress<ProjectTransferProgress> progress = new(UpdateProjectTransferProgress);
                FolderImportResult result = await _projectService.ImportFolderAsync(sourceFolder, progress);

                LoadProject(result.Project);
                StatusText = F("Status.ImportedFolder", result.ImageCount, result.ReanimCount, result.ParticleCount, result.TrailCount, result.MissingImageCount);
            }
            catch (Exception ex)
            {
                StatusText = F("Status.CouldNotImportFolder", ex.Message);
            }
            finally
            {
                EndProjectTransfer();
            }
        }

        public async Task ImportResourceFileAsync(string sourceFileName, Stream sourceStream)
        {
            if (sourceStream is null)
            {
                return;
            }

            if (!CanModifyCurrentProject)
            {
                StatusText = T("Status.CreateWritableProjectForImport");
                return;
            }

            if (!await ConfirmAllUnsavedChangesAsync())
            {
                StatusText = T("Status.CanceledResourceImport");
                return;
            }

            try
            {
                ProjectResourceResult result = await _projectService.ImportResourceFileAsync(CurrentProject, sourceFileName, sourceStream);
                RefreshCurrentProject();
                OpenResourceEditor(result.Kind, result.AssetId);
                StatusText = F("Status.ImportedResource", result.Kind, result.AssetId);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
            {
                StatusText = F("Status.CouldNotImportResource", ex.Message);
            }
        }

        [RelayCommand]
        private void ShowNewResourceDialog()
        {
            if (!CanModifyCurrentProject)
            {
                StatusText = T("Status.CreateWritableProjectForResource");
                return;
            }

            NewResourceKind = EffectAssetKind.Reanim;
            NewResourceId = "new_reanim";
            IsNewResourceDialogOpen = true;
            StatusText = T("Status.CreatingNewResource");
        }

        [RelayCommand]
        private async Task CreateResourceAsync()
        {
            if (!CanModifyCurrentProject)
            {
                StatusText = T("Status.CreateWritableProjectForResource");
                return;
            }

            string assetId = string.IsNullOrWhiteSpace(NewResourceId)
                ? NewResourceKind.ToString().ToLowerInvariant()
                : NewResourceId.Trim();

            IsNewResourceDialogOpen = false;
            if (!await ConfirmAllUnsavedChangesAsync())
            {
                StatusText = T("Status.CanceledCreatingResource");
                IsNewResourceDialogOpen = true;
                return;
            }

            try
            {
                ProjectResourceResult result = await _projectService.CreateResourceAsync(CurrentProject, NewResourceKind, assetId);
                RefreshCurrentProject();
                OpenResourceEditor(result.Kind, result.AssetId);
                StatusText = F("Status.CreatedResource", result.Kind, result.AssetId);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
            {
                StatusText = F("Status.CouldNotCreateResource", ex.Message);
            }
        }

        [RelayCommand]
        private void CancelNewResource()
        {
            IsNewResourceDialogOpen = false;
            StatusText = T("Status.CanceledCreatingResource");
        }

        public async Task ImportProjectZipAsync(Stream zipStream)
        {
            if (zipStream is null)
            {
                return;
            }

            if (!await ConfirmAllUnsavedChangesAsync())
            {
                StatusText = T("Status.CanceledProjectImport");
                return;
            }

            try
            {
                BeginProjectTransfer(T("Transfer.ImportingProject"), T("Transfer.ReadingArchive"));
                Progress<ProjectTransferProgress> progress = new(UpdateProjectTransferProgress);
                EffectProject project = await _projectService.ImportProjectZipAsync(zipStream, progress);
                LoadProject(project);
                StatusText = F("Status.ImportedProject", project.Manifest.Name);
            }
            catch (System.Exception ex) when (ex is IOException or System.IO.InvalidDataException or System.Text.Json.JsonException or System.InvalidOperationException)
            {
                StatusText = F("Status.CouldNotImportProject", ex.Message);
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
                StatusText = T("Status.NoInternalProjectLoaded");
                return;
            }

            foreach (EditorViewModelBase editor in OpenEditors.Where(editor => editor.IsDirty).ToList())
            {
                if (!await SaveEditorAsync(editor))
                {
                    StatusText = F("Status.CanceledExporting", CurrentProject.Manifest.Name);
                    return;
                }
            }

            try
            {
                BeginProjectTransfer(T("Transfer.ExportingProject"), T("Transfer.PreparingArchive"));
                Progress<ProjectTransferProgress> progress = new(UpdateProjectTransferProgress);
                await _projectService.ExportProjectZipAsync(CurrentProject, outputStream, progress);
                StatusText = F("Status.Exported", CurrentProject.Manifest.Name);
            }
            catch (System.Exception ex) when (ex is IOException or System.UnauthorizedAccessException or System.InvalidOperationException)
            {
                StatusText = F("Status.CouldNotExportProject", ex.Message);
            }
            finally
            {
                EndProjectTransfer();
            }
        }

        public string GetSelectedFileExportName()
        {
            if (SelectedEditor is EffectEditorViewModel effectEditor)
            {
                return EffectFileFormatUtility.GetExportFileName(effectEditor.Kind, effectEditor.AssetId, EffectFileFormat.Source);
            }

            string exportPath = SelectedEditor?.ExportPath;
            string fileName = string.IsNullOrWhiteSpace(exportPath)
                ? SelectedEditor?.Title
                : Path.GetFileName(exportPath);

            return CreateSafeFileName(fileName, "effectviewer-file");
        }

        public string GetSelectedFileDefaultExtension()
        {
            string extension = Path.GetExtension(GetSelectedFileExportName());
            return string.IsNullOrWhiteSpace(extension) ? null : extension.TrimStart('.');
        }

        public IReadOnlyList<string> GetSelectedFileExportPatterns()
        {
            if (SelectedEditor is EffectEditorViewModel effectEditor)
            {
                return
                [
                    "*" + EffectFileFormatUtility.GetSourceExtension(effectEditor.Kind),
                    "*" + EffectFileFormatUtility.GetCompiledSuffix(effectEditor.Kind)
                ];
            }

            string extension = Path.GetExtension(GetSelectedFileExportName());
            return string.IsNullOrWhiteSpace(extension) ? ["*.*"] : ["*" + extension];
        }

        public async Task<bool> PrepareSelectedFileExportAsync()
        {
            EditorViewModelBase editor = SelectedEditor;
            if (editor is null)
            {
                StatusText = T("Status.NoDocumentSelected");
                return false;
            }

            if (!editor.SupportsFileExport)
            {
                StatusText = F("Status.DocumentNoProjectFile", editor.Title);
                return false;
            }

            if (CurrentProject is null || string.IsNullOrWhiteSpace(CurrentProject.RootPath))
            {
                StatusText = T("Status.NoInternalProjectLoaded");
                return false;
            }

            if (editor.IsDirty && !await SaveEditorAsync(editor))
            {
                StatusText = F("Status.CanceledExporting", editor.Title);
                return false;
            }

            return true;
        }

        public async Task ExportSelectedFileAsync(Stream outputStream, string targetFileName)
        {
            if (outputStream is null || SelectedEditor is null)
            {
                return;
            }

            EditorViewModelBase editor = SelectedEditor;

            try
            {
                await editor.ExportAsync(_projectService, CurrentProject, outputStream, targetFileName);
                StatusText = F("Status.Exported", editor.Title);
            }
            catch (System.Exception ex) when (ex is IOException or System.UnauthorizedAccessException or System.InvalidOperationException)
            {
                StatusText = F("Status.CouldNotExport", editor.Title, ex.Message);
            }
        }

        private void BeginProjectTransfer(string title, string message)
        {
            ProjectTransferTitle = title;
            ProjectTransferMessage = message;
            ProjectTransferProgressValue = 0d;
            IsProjectTransferProgressIndeterminate = true;
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
            IsProjectTransferProgressIndeterminate = progress.TotalItems <= 0;
            ProjectTransferProgressText = progress.TotalItems <= 0
                ? progress.CompletedItems > 0 ? F("Transfer.CompletedScanned", progress.CompletedItems) : string.Empty
                : $"{progress.CompletedItems} / {progress.TotalItems}";
        }

        private void EndProjectTransfer()
        {
            IsProjectTransferInProgress = false;
            ProjectTransferProgressValue = 0d;
            IsProjectTransferProgressIndeterminate = false;
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
                ? T("Status.OpenProjectMessageHasProjects")
                : T("Status.OpenProjectMessageEmpty");
            IsOpenProjectDialogOpen = true;
            StatusText = HasAvailableProjects
                ? F("Status.FoundProjects", AvailableProjects.Count)
                : T("Status.NoInternalProjectsFound");
        }

        [RelayCommand]
        private async Task OpenSelectedProjectAsync()
        {
            ProjectListItemViewModel projectItem = SelectedAvailableProject;
            if (projectItem is null || string.IsNullOrWhiteSpace(projectItem.ProjectPath))
            {
                StatusText = T("Status.NoProjectSelected");
                return;
            }

            IsOpenProjectDialogOpen = false;
            if (!await ConfirmAllUnsavedChangesAsync())
            {
                StatusText = T("Status.CanceledOpeningProject");
                IsOpenProjectDialogOpen = true;
                return;
            }

            EffectProject project = await _projectService.LoadAsync(projectItem.ProjectPath);
            LoadProject(project);
            StatusText = F("Status.OpenedProject", project.Manifest.Name);
        }

        [RelayCommand]
        private void CancelOpenProject()
        {
            IsOpenProjectDialogOpen = false;
            SelectedAvailableProject = null;
            StatusText = T("Status.CanceledOpeningProject");
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

        private void RefreshCurrentProject()
        {
            CurrentProject?.RebuildAssetIndex();
            _effectWorld.LoadProject(CurrentProject);
            _luaHost = new LuaHost(_effectWorld);
            SelectedProjectItem = null;
            RebuildProjectTree();
            OnPropertyChanged(nameof(CurrentProject));
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
                        StatusText = F("Status.EditingImage", image.Id);
                    }
                    break;

                case EffectAssetKind.Reanim:
                    OpenOrSelectEditor(item.Kind, item.AssetId, () => new EffectEditorViewModel(item.Kind, item.AssetId, item.Path, CurrentProject));
                    StatusText = F("Status.EditingReanim", item.AssetId);
                    break;

                case EffectAssetKind.Particle:
                    OpenOrSelectEditor(item.Kind, item.AssetId, () => new EffectEditorViewModel(item.Kind, item.AssetId, item.Path, CurrentProject));
                    StatusText = F("Status.EditingParticle", item.AssetId);
                    break;

                case EffectAssetKind.Trail:
                    OpenOrSelectEditor(item.Kind, item.AssetId, () => new EffectEditorViewModel(item.Kind, item.AssetId, item.Path, CurrentProject));
                    StatusText = F("Status.EditingTrail", item.AssetId);
                    break;

                case EffectAssetKind.Showcase:
                    if (CurrentProject.Assets.Showcases.TryGetValue(item.AssetId, out ShowcaseAsset showcase))
                    {
                        OpenOrSelectEditor(item.Kind, item.AssetId, () => new ShowcaseEditorViewModel(showcase, _luaHost, CurrentProject));
                        StatusText = F("Status.EditingShowcase", item.AssetId);
                    }
                    else
                    {
                        OpenOrSelectEditor(item.Kind, item.AssetId, () => new ShowcaseEditorViewModel(_luaHost, CurrentProject));
                        StatusText = T("Status.EditingShowcases");
                    }
                    break;
            }
        }

        private void OpenResourceEditor(EffectAssetKind kind, string assetId)
        {
            EffectAsset asset = kind switch
            {
                EffectAssetKind.Reanim when CurrentProject.Assets.Reanims.TryGetValue(assetId, out ReanimAsset reanim) => reanim,
                EffectAssetKind.Particle when CurrentProject.Assets.Particles.TryGetValue(assetId, out EffectAsset particle) => particle,
                EffectAssetKind.Trail when CurrentProject.Assets.Trails.TryGetValue(assetId, out EffectAsset trail) => trail,
                _ => null
            };

            if (kind == EffectAssetKind.Image &&
                CurrentProject.Assets.Images.TryGetValue(assetId, out ImageAsset image))
            {
                OpenOrSelectEditor(kind, assetId, () => new ImageEditorViewModel(image, CurrentProject));
                return;
            }

            if (kind == EffectAssetKind.Showcase &&
                CurrentProject.Assets.Showcases.TryGetValue(assetId, out ShowcaseAsset showcase))
            {
                OpenOrSelectEditor(kind, assetId, () => new ShowcaseEditorViewModel(showcase, _luaHost, CurrentProject));
                return;
            }

            if (asset is not null)
            {
                OpenOrSelectEditor(kind, assetId, () => new EffectEditorViewModel(kind, assetId, asset.Path, CurrentProject));
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
                StatusText = F("Status.CanceledClosingDocument", editor.Title);
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
                    StatusText = F("Status.DiscardedChanges", editor.Title);
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
                StatusText = F("Status.DocumentSaveUnsupported", editor.Title);
                return false;
            }

            if (CurrentProject is null || string.IsNullOrWhiteSpace(CurrentProject.RootPath))
            {
                StatusText = T("Status.NoWritableProjectLoaded");
                return false;
            }

            try
            {
                await editor.SaveAsync(_projectService, CurrentProject);
                StatusText = F("Status.Saved", editor.Title);
                return true;
            }
            catch (System.Exception ex) when (ex is IOException or System.FormatException or System.InvalidOperationException)
            {
                StatusText = F("Status.CouldNotSave", editor.Title, ex.Message);
                return false;
            }
        }

        private Task<UnsavedChangesChoice> PromptUnsavedChangesAsync(EditorViewModelBase editor)
        {
            _unsavedChangesCompletion = new TaskCompletionSource<UnsavedChangesChoice>();
            UnsavedChangesTitle = T("Dialog.UnsavedChangesTitle");
            UnsavedChangesMessage = F("Dialog.UnsavedChangesMessage", editor.Title);
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

            OnPropertyChanged(nameof(CanSaveSelectedFile));
            OnPropertyChanged(nameof(CanExportSelectedFile));

            if (value is not null)
            {
                StatusText = value.IsDirty
                    ? F("Status.EditingUnsaved", value.Title)
                    : F("Status.Editing", value.Title);
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
                    ? F("Status.EditingUnsaved", editor.Title)
                    : F("Status.Editing", editor.Title);
            }

            if (editor is ImageEditorViewModel imageEditor &&
                e.PropertyName == nameof(ImageEditorViewModel.AssetId))
            {
                UpdateProjectExplorerItemIdentity(
                    EffectAssetKind.Image,
                    imageEditor.SavedAssetId,
                    imageEditor.AssetId,
                    imageEditor.Path);
            }
            else if (editor is EffectEditorViewModel effectEditor &&
                e.PropertyName == nameof(EffectEditorViewModel.AssetId))
            {
                UpdateProjectExplorerItemIdentity(
                    effectEditor.Kind,
                    effectEditor.AssetId,
                    effectEditor.AssetId,
                    effectEditor.Path);
            }
        }

        private void UpdateProjectExplorerItemIdentity(
            EffectAssetKind kind,
            string oldAssetId,
            string newAssetId,
            string path)
        {
            ProjectExplorerItemViewModel item = FindProjectExplorerItem(ProjectItems, kind, oldAssetId, path);
            if (item is null)
            {
                return;
            }

            item.Title = newAssetId;
            item.AssetId = newAssetId;
            item.Path = path;
            if (ReferenceEquals(SelectedProjectItem, item))
            {
                OnPropertyChanged(nameof(SelectedProjectItem));
            }
        }

        private static ProjectExplorerItemViewModel FindProjectExplorerItem(
            IEnumerable<ProjectExplorerItemViewModel> items,
            EffectAssetKind kind,
            string assetId,
            string path)
        {
            foreach (ProjectExplorerItemViewModel item in items)
            {
                if (item.Kind == kind &&
                    (string.Equals(item.AssetId, assetId, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase)))
                {
                    return item;
                }

                ProjectExplorerItemViewModel child = FindProjectExplorerItem(item.Children, kind, assetId, path);
                if (child is not null)
                {
                    return child;
                }
            }

            return null;
        }

        private void RebuildProjectTree()
        {
            ProjectItems.Clear();

            ProjectExplorerItemViewModel root = new(CurrentProject.Manifest.Name, EffectAssetKind.Project)
            {
                IsExpanded = true
            };

            root.Children.Add(CreateFolder(T("ProjectTree.Images"), CurrentProject.Manifest.Images.Select(asset =>
                new ProjectExplorerItemViewModel(asset.Id, EffectAssetKind.Image, asset.Id, asset.Path))));

            root.Children.Add(CreateFolder(T("ProjectTree.Reanim"), CurrentProject.Manifest.Reanims.Select(asset =>
                new ProjectExplorerItemViewModel(asset.Id, EffectAssetKind.Reanim, asset.Id, asset.Path))));

            root.Children.Add(CreateFolder(T("ProjectTree.Particles"), CurrentProject.Manifest.Particles.Select(asset =>
                new ProjectExplorerItemViewModel(asset.Id, EffectAssetKind.Particle, asset.Id, asset.Path))));

            root.Children.Add(CreateFolder(T("ProjectTree.Trails"), CurrentProject.Manifest.Trails.Select(asset =>
                new ProjectExplorerItemViewModel(asset.Id, EffectAssetKind.Trail, asset.Id, asset.Path))));

            ProjectExplorerItemViewModel showcases = new(T("ProjectTree.Showcases"), EffectAssetKind.Showcase, "Showcases")
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

        private static string CreateSafeFileName(string value, string fallback)
        {
            string name = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(name) ? fallback : name;
        }
    }
}
