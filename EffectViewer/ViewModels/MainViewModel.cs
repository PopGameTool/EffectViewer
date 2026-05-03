using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EffectViewer.Assets;
using EffectViewer.Localization;
using EffectViewer.Projects;
using EffectViewer.Rendering;
using EffectViewer.Rendering.Export;
using EffectViewer.Rendering.TextureUpload;
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
        private ProjectListItemViewModel _projectBeingRenamed;
        private ProjectListItemViewModel _projectBeingDeleted;
        private ProjectExplorerItemViewModel _resourceBeingDeleted;
        private static readonly StringComparer ProjectTreeNameComparer = StringComparer.OrdinalIgnoreCase;
        private const string ImagesProjectTreeGroupKey = "images";
        private const string ReanimsProjectTreeGroupKey = "reanims";
        private const string ParticlesProjectTreeGroupKey = "particles";
        private const string TrailsProjectTreeGroupKey = "trails";
        private const string ShowcasesProjectTreeGroupKey = "showcases";
        private const int MaxRecentlyOpenedProjectItems = 8;
        private const int ProjectTreeSearchDebounceMilliseconds = 250;
        private readonly List<ProjectExplorerResourceIdentity> _recentlyOpenedResources = [];
        private readonly Dictionary<string, bool> _projectTreeExpansionState = new(StringComparer.OrdinalIgnoreCase);
        private string _appliedProjectTreeSearchText = string.Empty;
        private CancellationTokenSource _projectTreeSearchRefreshCancellation;

        public ObservableCollection<ProjectExplorerItemViewModel> ProjectItems { get; } = [];
        public ObservableCollection<ProjectExplorerItemViewModel> RecentlyOpenedProjectItems { get; } = [];
        public ObservableCollection<EditorViewModelBase> OpenEditors { get; } = [];
        public ObservableCollection<ProjectListItemViewModel> AvailableProjects { get; } = [];

        [ObservableProperty]
        private EffectProject _currentProject;

        [ObservableProperty]
        private ProjectExplorerItemViewModel _selectedProjectItem;

        [ObservableProperty]
        private string _projectTreeSearchText = string.Empty;

        [ObservableProperty]
        private int _projectTreeResourceCount;

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
        private bool _isRenameProjectDialogOpen;

        [ObservableProperty]
        private string _renameProjectName;

        [ObservableProperty]
        private bool _isDeleteProjectDialogOpen;

        [ObservableProperty]
        private string _deleteProjectMessage;

        [ObservableProperty]
        private bool _isDeleteResourceDialogOpen;

        [ObservableProperty]
        private string _deleteResourceMessage;

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
        private bool _useLightViewportBackground;

        [ObservableProperty]
        private bool _useDarkViewportBackground;

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

        [ObservableProperty]
        private bool _isPreviewExportDialogOpen;

        [ObservableProperty]
        private PreviewExportFormat _previewExportFormat = PreviewExportFormat.Png;

        [ObservableProperty]
        private double _previewExportCanvasScale = 1d;

        [ObservableProperty]
        private int _previewExportFps = 30;

        [ObservableProperty]
        private double _previewExportDurationSeconds = 2d;

        [ObservableProperty]
        private double _previewExportStartSeconds;

        [ObservableProperty]
        private double _previewExportEndSeconds = 2d;

        [ObservableProperty]
        private bool _previewExportUntilEnd;

        [ObservableProperty]
        private int _previewExportStartFrame = 1;

        [ObservableProperty]
        private int _previewExportEndFrame = 1;

        [ObservableProperty]
        private PreviewExportTimelineOption _selectedPreviewExportTimeline;

        private bool _suppressPreviewExportTimelineRangeUpdate;

        public IReadOnlyList<EffectAssetKind> NewResourceKinds { get; } =
        [
            EffectAssetKind.Reanim,
            EffectAssetKind.Particle,
            EffectAssetKind.Trail,
            EffectAssetKind.Showcase
        ];

        public IReadOnlyList<PreviewExportFormat> PreviewExportFormats { get; } =
        [
            PreviewExportFormat.Png,
            PreviewExportFormat.PngSequenceZip,
            PreviewExportFormat.Gif,
            PreviewExportFormat.Webp
        ];

        public ObservableCollection<PreviewExportTimelineOption> PreviewExportTimelines { get; } = [];

        public bool IsProjectExplorerDockedLeft => ProjectExplorerDockSide == ProjectExplorerDockSide.Left;
        public bool IsProjectExplorerDockedRight => ProjectExplorerDockSide == ProjectExplorerDockSide.Right;
        public bool IsProjectExplorerVisibleLeft => IsProjectExplorerVisible && IsProjectExplorerDockedLeft;
        public bool IsProjectExplorerVisibleRight => IsProjectExplorerVisible && IsProjectExplorerDockedRight;
        public bool HasAvailableProjects => AvailableProjects.Count > 0;
        public bool CanSaveCurrentProject => CurrentProject is not null && !string.IsNullOrWhiteSpace(CurrentProject.RootPath);
        public bool CanSaveSelectedFile => CanSaveCurrentProject && SelectedEditor?.SupportsSave == true;
        public bool CanExportSelectedFile => CanSaveCurrentProject && SelectedEditor?.SupportsFileExport == true;
        public bool CanExportSelectedPreview => SelectedEditor is not null && SelectedEditor.Kind != EffectAssetKind.Project;
        public bool CanModifyCurrentProject => CanSaveCurrentProject;
        public bool CanDeleteSelectedResource => CanDeleteResourceItem(SelectedProjectItem);
        public bool IsEnglishLanguage => Loc.IsEnglish;
        public bool IsChineseLanguage => Loc.IsChinese;
        public bool HasOpenEditors => OpenEditors.Count > 0;
        public bool HasMultipleOpenEditors => OpenEditors.Count > 1;
        public bool HasClosableEditors => OpenEditors.Any(editor => editor.CanClose);
        public bool CanCloseSelectedEditor => SelectedEditor?.CanClose == true;
        public bool CanCloseOtherEditors => SelectedEditor is not null &&
            OpenEditors.Any(editor => editor.CanClose && !ReferenceEquals(editor, SelectedEditor));
        public bool CanSelectPreviousEditor => OpenEditors.Count > 1;
        public bool CanSelectNextEditor => OpenEditors.Count > 1;
        public bool CanMoveSelectedEditorLeft => CanMoveEditorLeft(SelectedEditor);
        public bool CanMoveSelectedEditorRight => CanMoveEditorRight(SelectedEditor);
        public bool CanSaveAnyEditor => CanSaveCurrentProject && OpenEditors.Any(editor => editor.IsDirty && editor.SupportsSave);
        public bool CanCloseSavedEditors => OpenEditors.Any(editor => editor.CanClose && !editor.IsDirty);
        public bool HasProjectTreeSearchText => !string.IsNullOrWhiteSpace(ProjectTreeSearchText);
        public bool HasVisibleProjectTreeItems => ProjectItems.Count > 0;
        public bool HasNoVisibleProjectTreeItems => !HasVisibleProjectTreeItems;
        public bool HasRecentlyOpenedProjectItems => RecentlyOpenedProjectItems.Count > 0;
        public string ProjectTreeEmptyMessage => CurrentProject is null
            ? T("Status.NoProjectLoaded")
            : ProjectTreeResourceCount == 0
                ? T("ProjectTree.NoResources")
                : T("ProjectTree.NoMatches");
        public bool IsPreviewAnimationExport => PreviewExportFormat is PreviewExportFormat.PngSequenceZip or PreviewExportFormat.Gif or PreviewExportFormat.Webp;
        public bool IsPreviewExportParticle => SelectedEditor?.Kind == EffectAssetKind.Particle;
        public bool HasPreviewExportTimelineOptions => PreviewExportTimelines.Count > 0;
        public bool HasPreviewExportFrameRangeOptions => PreviewExportTimelines.Count > 0;
        public bool HasPreviewExportTimeRangeOptions => SelectedEditor?.Kind is EffectAssetKind.Particle or EffectAssetKind.Trail;
        public bool IsPreviewExportEndFrameEditable => HasPreviewExportFrameRangeOptions && IsPreviewAnimationExport;
        public bool IsPreviewExportEndSecondsEditable => HasPreviewExportTimeRangeOptions && IsPreviewAnimationExport && !PreviewExportUntilEnd;
        public bool IsPreviewExportUntilEndEditable => HasPreviewExportTimeRangeOptions && IsPreviewAnimationExport;
        public bool IsPreviewExportDurationVisible => IsPreviewAnimationExport && !HasPreviewExportFrameRangeOptions && !HasPreviewExportTimeRangeOptions;
        public int PreviewExportFrameRangeMaximum => PreviewExportTimelines.Count == 0
            ? 1
            : PreviewExportTimelines.Max(option => Math.Max(option.EndFrameNumber, 1));
        public int PreviewExportFrameCount => CreatePreviewExportOptions().FrameCount;
        public string PreviewExportFrameCountSummary => CreatePreviewExportOptions().StopOnProviderCompletion
            ? F("PreviewExport.FrameCountUpTo", PreviewExportFrameCount)
            : F("PreviewExport.FrameCount", PreviewExportFrameCount);

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
            OpenEditors.CollectionChanged += OnOpenEditorsCollectionChanged;
            ShowWelcomePage();
            StatusText = T("Status.NoProjectLoaded");
        }

        public event EventHandler ImportResourceFolderRequested;
        public event EventHandler PreviewExportRequested;

        private static LocalizationManager Loc => LocalizationManager.Instance;
        private static string T(string key) => Loc.Text(key);
        private static string F(string key, params object[] args) => Loc.Format(key, args);

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(IsEnglishLanguage));
            OnPropertyChanged(nameof(IsChineseLanguage));

            RebuildRecentlyOpenedProjectItems();
            NotifyProjectTreeProperties();

            if (CurrentProject is not null)
            {
                RebuildProjectTree();
            }

            if (IsOpenProjectDialogOpen)
            {
                OpenProjectMessage = HasAvailableProjects
                    ? T("Status.OpenProjectMessageHasProjects")
                    : T("Status.OpenProjectMessageEmpty");
            }

            if (IsDeleteProjectDialogOpen && _projectBeingDeleted is not null)
            {
                DeleteProjectMessage = F(
                    "Dialog.DeleteProjectMessage",
                    _projectBeingDeleted.Name,
                    _projectBeingDeleted.DirectoryName);
            }

            if (IsDeleteResourceDialogOpen && _resourceBeingDeleted is not null)
            {
                DeleteResourceMessage = F(
                    "Dialog.DeleteResourceMessage",
                    _resourceBeingDeleted.Kind,
                    _resourceBeingDeleted.AssetId);
            }

            if (IsUnsavedChangesPromptOpen && SelectedEditor is not null)
            {
                UnsavedChangesTitle = T("Dialog.UnsavedChangesTitle");
                UnsavedChangesMessage = F("Dialog.UnsavedChangesMessage", SelectedEditor.Title);
            }

            RefreshPreviewExportTimelineOptions();
            NotifyPreviewExportProperties();
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

        partial void OnUseLightViewportBackgroundChanged(bool value)
        {
            if (value)
            {
                UseDarkViewportBackground = false;
            }

            ApplyViewportBackgroundMode();
        }

        partial void OnUseDarkViewportBackgroundChanged(bool value)
        {
            if (value)
            {
                UseLightViewportBackground = false;
            }

            ApplyViewportBackgroundMode();
        }

        private void ApplyViewportBackgroundMode()
        {
            ViewportBackgroundSettings.Mode = UseLightViewportBackground
                ? ViewportBackgroundMode.Light
                : UseDarkViewportBackground
                    ? ViewportBackgroundMode.Dark
                    : ViewportBackgroundMode.Theme;
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
            OnPropertyChanged(nameof(CanDeleteSelectedResource));
            OnPropertyChanged(nameof(CanSaveAnyEditor));
            OnPropertyChanged(nameof(ProjectTreeEmptyMessage));
        }

        partial void OnProjectTreeSearchTextChanged(string value)
        {
            OnPropertyChanged(nameof(HasProjectTreeSearchText));
            ScheduleProjectTreeSearchRefresh(value);
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

        [RelayCommand]
        private void RequestImportResourceFolder()
        {
            ImportResourceFolderRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void ClearProjectTreeSearch()
        {
            ProjectTreeSearchText = string.Empty;
        }

        [RelayCommand]
        private void OpenProjectExplorerItem(ProjectExplorerItemViewModel item)
        {
            if (item?.IsSelectable != true)
            {
                return;
            }

            ProjectExplorerItemViewModel visibleItem = FindProjectExplorerItem(ProjectItems, item.Kind, item.AssetId, item.Path);
            if (visibleItem is not null)
            {
                SelectedProjectItem = visibleItem;
                return;
            }

            OpenEditor(item);
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
                RefreshCurrentProjectState();
                AddOrUpdateProjectExplorerItem(result.Kind, result.AssetId, result.ProjectPath);
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
                RefreshCurrentProjectState();
                AddOrUpdateProjectExplorerItem(result.Kind, result.AssetId, result.ProjectPath);
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

        [RelayCommand]
        private void ShowDeleteSelectedResourceDialog()
        {
            ShowDeleteResourceDialog(SelectedProjectItem);
        }

        [RelayCommand]
        private void ShowDeleteResourceDialog(ProjectExplorerItemViewModel item)
        {
            item ??= SelectedProjectItem;
            if (!CanDeleteResourceItem(item))
            {
                StatusText = T("Status.NoResourceSelected");
                return;
            }

            _resourceBeingDeleted = item;
            SelectedProjectItem = item;
            DeleteResourceMessage = F("Dialog.DeleteResourceMessage", item.Kind, item.AssetId);
            IsDeleteResourceDialogOpen = true;
            StatusText = F("Status.DeletingResource", item.Kind, item.AssetId);
        }

        [RelayCommand]
        private async Task DeleteResourceAsync()
        {
            ProjectExplorerItemViewModel item = _resourceBeingDeleted;
            if (!CanDeleteResourceItem(item))
            {
                StatusText = T("Status.NoResourceSelected");
                return;
            }

            IsDeleteResourceDialogOpen = false;

            EditorViewModelBase editor = FindOpenEditor(item.Kind, item.AssetId);
            if (!await ConfirmUnsavedChangesAsync(editor))
            {
                StatusText = T("Status.CanceledDeletingResource");
                IsDeleteResourceDialogOpen = true;
                return;
            }

            EffectAssetKind kind = item.Kind;
            string assetId = item.AssetId;
            string path = item.Path;

            try
            {
                ProjectResourceResult result = await _projectService.DeleteResourceAsync(CurrentProject, kind, assetId, path);
                RefreshCurrentProjectState();
                CloseResourceEditors(result.Kind, result.AssetId);
                RemoveRecentlyOpenedResource(result.Kind, result.AssetId, result.ProjectPath);
                RebuildProjectTree();
                SelectedProjectItem = null;
                _resourceBeingDeleted = null;
                DeleteResourceMessage = string.Empty;
                StatusText = F("Status.DeletedResource", result.Kind, result.AssetId);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
            {
                StatusText = F("Status.CouldNotDeleteResource", ex.Message);
                IsDeleteResourceDialogOpen = true;
            }
        }

        [RelayCommand]
        private void CancelDeleteResource()
        {
            IsDeleteResourceDialogOpen = false;
            _resourceBeingDeleted = null;
            DeleteResourceMessage = string.Empty;
            StatusText = T("Status.CanceledDeletingResource");
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

        [RelayCommand]
        private void ShowPreviewExportDialog()
        {
            if (!CanExportSelectedPreview)
            {
                StatusText = T("Status.NoDocumentSelected");
                return;
            }

            PreviewExportFps = SelectedEditor?.PreviewExportDefaultFps ?? 30;
            RefreshPreviewExportTimelineOptions(preferDefault: true);
            ApplySelectedPreviewExportTimelineFrameRange();
            IsPreviewExportDialogOpen = true;
            StatusText = F("Status.ExportingPreview", SelectedEditor.Title);
        }

        [RelayCommand]
        private void RequestPreviewExport()
        {
            if (!CanExportSelectedPreview)
            {
                StatusText = T("Status.NoDocumentSelected");
                return;
            }

            IsPreviewExportDialogOpen = false;
            PreviewExportRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void CancelPreviewExport()
        {
            IsPreviewExportDialogOpen = false;
            if (SelectedEditor is not null)
            {
                StatusText = F("Status.CanceledExporting", SelectedEditor.Title);
            }
        }

        partial void OnPreviewExportFormatChanged(PreviewExportFormat value)
        {
            NotifyPreviewExportProperties();
        }

        partial void OnPreviewExportFpsChanged(int value)
        {
            PreviewExportFps = Math.Clamp(value, 1, 240);
            NotifyPreviewExportProperties();
        }

        partial void OnPreviewExportDurationSecondsChanged(double value)
        {
            PreviewExportDurationSeconds = Math.Clamp(value, 0.01d, 600d);
            NotifyPreviewExportProperties();
        }

        partial void OnPreviewExportStartSecondsChanged(double value)
        {
            double clamped = ClampPreviewExportSeconds(value);
            if (!ApproximatelyEqual(value, clamped))
            {
                PreviewExportStartSeconds = clamped;
                return;
            }

            if (!PreviewExportUntilEnd && PreviewExportEndSeconds < clamped)
            {
                PreviewExportEndSeconds = clamped;
                return;
            }

            NotifyPreviewExportProperties();
        }

        partial void OnPreviewExportEndSecondsChanged(double value)
        {
            double clamped = ClampPreviewExportSeconds(value);
            if (!ApproximatelyEqual(value, clamped))
            {
                PreviewExportEndSeconds = clamped;
                return;
            }

            NotifyPreviewExportProperties();
        }

        partial void OnPreviewExportUntilEndChanged(bool value)
        {
            if (!value && PreviewExportEndSeconds < PreviewExportStartSeconds)
            {
                PreviewExportEndSeconds = PreviewExportStartSeconds;
                return;
            }

            NotifyPreviewExportProperties();
        }

        partial void OnPreviewExportCanvasScaleChanged(double value)
        {
            PreviewExportCanvasScale = Math.Clamp(value, 0.1d, 16d);
        }

        partial void OnPreviewExportStartFrameChanged(int value)
        {
            int clamped = ClampPreviewExportFrame(value);
            if (value != clamped)
            {
                PreviewExportStartFrame = clamped;
                return;
            }

            if (PreviewExportEndFrame < clamped)
            {
                PreviewExportEndFrame = clamped;
                return;
            }

            NotifyPreviewExportProperties();
        }

        partial void OnPreviewExportEndFrameChanged(int value)
        {
            int clamped = ClampPreviewExportFrame(value);
            if (value != clamped)
            {
                PreviewExportEndFrame = clamped;
                return;
            }

            NotifyPreviewExportProperties();
        }

        partial void OnSelectedPreviewExportTimelineChanged(PreviewExportTimelineOption value)
        {
            if (!_suppressPreviewExportTimelineRangeUpdate)
            {
                ApplyPreviewExportTimelineFrameRange(value);
            }

            NotifyPreviewExportProperties();
        }

        private void NotifyPreviewExportProperties()
        {
            OnPropertyChanged(nameof(IsPreviewAnimationExport));
            OnPropertyChanged(nameof(IsPreviewExportParticle));
            OnPropertyChanged(nameof(HasPreviewExportTimelineOptions));
            OnPropertyChanged(nameof(HasPreviewExportFrameRangeOptions));
            OnPropertyChanged(nameof(HasPreviewExportTimeRangeOptions));
            OnPropertyChanged(nameof(IsPreviewExportEndFrameEditable));
            OnPropertyChanged(nameof(IsPreviewExportEndSecondsEditable));
            OnPropertyChanged(nameof(IsPreviewExportUntilEndEditable));
            OnPropertyChanged(nameof(IsPreviewExportDurationVisible));
            OnPropertyChanged(nameof(PreviewExportFrameRangeMaximum));
            OnPropertyChanged(nameof(PreviewExportFrameCount));
            OnPropertyChanged(nameof(PreviewExportFrameCountSummary));
        }

        public string GetPreviewExportFileName()
        {
            string baseName = CreateSafeFileName(SelectedEditor?.Title, "effectviewer-preview");
            if (HasPreviewExportFrameRangeOptions)
            {
                baseName = IsPreviewAnimationExport
                    ? $"{baseName}-f{PreviewExportStartFrame:0000}-{PreviewExportEndFrame:0000}"
                    : $"{baseName}-f{PreviewExportStartFrame:0000}";
            }
            else if (HasPreviewExportTimeRangeOptions)
            {
                string start = FormatPreviewExportSecondsForFileName(PreviewExportStartSeconds);
                baseName = IsPreviewAnimationExport
                    ? PreviewExportUntilEnd
                        ? $"{baseName}-t{start}s-end"
                        : $"{baseName}-t{start}s-{FormatPreviewExportSecondsForFileName(PreviewExportEndSeconds)}s"
                    : $"{baseName}-t{start}s";
            }

            return PreviewExportFormat switch
            {
                PreviewExportFormat.PngSequenceZip => $"{baseName}-frames.zip",
                PreviewExportFormat.Gif => $"{baseName}.gif",
                PreviewExportFormat.Webp => $"{baseName}.webp",
                _ => $"{baseName}.png"
            };
        }

        public string GetPreviewExportDefaultExtension()
        {
            return PreviewExportFormat switch
            {
                PreviewExportFormat.PngSequenceZip => "zip",
                PreviewExportFormat.Gif => "gif",
                PreviewExportFormat.Webp => "webp",
                _ => "png"
            };
        }

        public IReadOnlyList<string> GetPreviewExportPatterns()
        {
            return PreviewExportFormat switch
            {
                PreviewExportFormat.PngSequenceZip => ["*.zip"],
                PreviewExportFormat.Gif => ["*.gif"],
                PreviewExportFormat.Webp => ["*.webp"],
                _ => ["*.png"]
            };
        }

        public async Task ExportSelectedPreviewAsync(Stream outputStream)
        {
            if (outputStream is null || !CanExportSelectedPreview)
            {
                return;
            }

            EditorViewModelBase editor = SelectedEditor;
            PreviewExportOptions options = CreatePreviewExportOptions().Normalized();
            ITextureSource textureSource = editor.TextureSource ?? new GeneratedTextureSource();
            IRenderFrameProvider exportProvider = null;
            bool ownsExportProvider = false;

            try
            {
                BeginProjectTransfer(T("Transfer.ExportingPreview"), F("Transfer.RenderingPreviewFrames", options.FrameCount));
                await Task.Yield();
                exportProvider = editor.CreatePreviewExportFrameProvider(null);
                ownsExportProvider = exportProvider is not null;
                exportProvider ??= editor.PreviewFrameProvider;
                Func<int, RenderFrame> frameFactory = CreatePreviewFrameFactory(editor.PreviewFrame, exportProvider, options);
                Func<bool> shouldStop = options.StopOnProviderCompletion && exportProvider is ICompletableRenderFrameProvider completableProvider
                    ? () => completableProvider.IsComplete
                    : null;
                Progress<PreviewExportProgress> progress = new(UpdatePreviewExportProgress);
                await Task.Run(() =>
                {
                    switch (options.Format)
                    {
                        case PreviewExportFormat.PngSequenceZip:
                            PreviewExportWriter.WritePngSequenceZip(outputStream, frameFactory, textureSource, options, shouldStop, progress);
                            break;

                        case PreviewExportFormat.Gif:
                            PreviewExportWriter.WriteGif(outputStream, frameFactory, textureSource, options, shouldStop, progress);
                            break;

                        case PreviewExportFormat.Webp:
                            PreviewExportWriter.WriteWebp(outputStream, frameFactory, textureSource, options, shouldStop, progress);
                            break;

                        default:
                            PreviewExportWriter.WritePng(outputStream, frameFactory(0), textureSource, options, progress);
                            break;
                    }
                });

                StatusText = F("Status.ExportedPreview", editor.Title);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
                StatusText = F("Status.CouldNotExportPreview", editor.Title, ex.Message);
            }
            finally
            {
                if (ownsExportProvider && exportProvider is IDisposable disposableProvider)
                {
                    disposableProvider.Dispose();
                }

                EndProjectTransfer();
            }
        }

        private PreviewExportOptions CreatePreviewExportOptions()
        {
            return new PreviewExportOptions
            {
                Format = PreviewExportFormat,
                CanvasScale = PreviewExportCanvasScale,
                Fps = PreviewExportFps,
                DurationSeconds = PreviewExportDurationSeconds,
                UseFrameRange = HasPreviewExportFrameRangeOptions,
                StartFrameIndex = Math.Max(0, PreviewExportStartFrame - 1),
                EndFrameIndex = Math.Max(0, PreviewExportEndFrame - 1),
                SourceFps = SelectedPreviewExportTimeline?.SourceFps ?? SelectedEditor?.PreviewExportDefaultFps ?? PreviewExportFps,
                UseTimeRange = HasPreviewExportTimeRangeOptions,
                StartTimeSeconds = PreviewExportStartSeconds,
                EndTimeSeconds = PreviewExportEndSeconds,
                ExportUntilComplete = PreviewExportUntilEnd && IsPreviewAnimationExport
            };
        }

        private void RefreshPreviewExportTimelineOptions(bool preferDefault = false)
        {
            string previousId = SelectedPreviewExportTimeline?.Id;
            bool previousWasFull = SelectedPreviewExportTimeline?.IsFullTimeline == true;
            _suppressPreviewExportTimelineRangeUpdate = true;
            try
            {
                PreviewExportTimelines.Clear();

                if (SelectedEditor is not null)
                {
                    foreach (PreviewExportTimelineOption option in SelectedEditor.GetPreviewExportTimelineOptions())
                    {
                        PreviewExportTimelines.Add(option);
                    }
                }

                string defaultId = SelectedEditor?.GetDefaultPreviewExportTimelineId();
                SelectedPreviewExportTimeline = preferDefault
                    ? FindPreviewExportTimeline(defaultId, string.Equals(defaultId, PreviewExportTimelineOption.FullTimelineId, StringComparison.OrdinalIgnoreCase)) ??
                        PreviewExportTimelines.FirstOrDefault()
                    : FindPreviewExportTimeline(previousId, previousWasFull) ??
                        FindPreviewExportTimeline(defaultId, string.Equals(defaultId, PreviewExportTimelineOption.FullTimelineId, StringComparison.OrdinalIgnoreCase)) ??
                        PreviewExportTimelines.FirstOrDefault();
            }
            finally
            {
                _suppressPreviewExportTimelineRangeUpdate = false;
            }

            NotifyPreviewExportProperties();
        }

        private PreviewExportTimelineOption FindPreviewExportTimeline(string id, bool isFullTimeline)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            return PreviewExportTimelines.FirstOrDefault(option =>
                option.IsFullTimeline == isFullTimeline &&
                string.Equals(option.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        private int ClampPreviewExportFrame(int value)
        {
            int maximum = Math.Max(1, PreviewExportFrameRangeMaximum);
            return Math.Clamp(value, 1, maximum);
        }

        private static double ClampPreviewExportSeconds(double value)
        {
            return Math.Clamp(value, 0d, PreviewExportOptions.MaximumTimeSeconds);
        }

        private static bool ApproximatelyEqual(double left, double right)
        {
            return Math.Abs(left - right) < 0.0005d;
        }

        private static string FormatPreviewExportSecondsForFileName(double seconds)
        {
            return ClampPreviewExportSeconds(seconds)
                .ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        private void ApplySelectedPreviewExportTimelineFrameRange()
        {
            ApplyPreviewExportTimelineFrameRange(SelectedPreviewExportTimeline);
        }

        private void ApplyPreviewExportTimelineFrameRange(PreviewExportTimelineOption option)
        {
            if (option is null || option.FrameCount <= 0)
            {
                PreviewExportStartFrame = 1;
                PreviewExportEndFrame = Math.Max(1, PreviewExportFrameRangeMaximum);
                return;
            }

            PreviewExportStartFrame = option.StartFrameNumber;
            PreviewExportEndFrame = option.EndFrameNumber;
        }

        private static Func<int, RenderFrame> CreatePreviewFrameFactory(RenderFrame staticFrame, IRenderFrameProvider provider, PreviewExportOptions options)
        {
            staticFrame ??= new RenderFrame();
            double deltaSeconds = 1d / Math.Clamp(options.Fps, 1, 240);
            int frameIndex = 0;
            bool firstFrame = true;

            return _ =>
            {
                if (provider is null)
                {
                    return staticFrame;
                }

                if (provider is ISeekableRenderFrameProvider seekableProvider)
                {
                    double elapsedSeconds = options.StartSeconds + frameIndex++ * deltaSeconds;
                    if (options.UseFrameRange || options.UseTimeRange)
                    {
                        elapsedSeconds = Math.Min(elapsedSeconds, options.EndSeconds);
                    }

                    return seekableProvider.GetFrameAtTime(elapsedSeconds);
                }

                double delta = firstFrame ? 0d : deltaSeconds;
                firstFrame = false;
                frameIndex++;
                return provider.GetFrame(delta);
            };
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

        private void UpdatePreviewExportProgress(PreviewExportProgress progress)
        {
            if (progress is null)
            {
                return;
            }

            string message = progress.Stage switch
            {
                PreviewExportProgressStage.CapturingFrames => F("Transfer.CapturingPreviewFrames", progress.CompletedItems, progress.TotalItems),
                PreviewExportProgressStage.RasterizingFrames => F("Transfer.RasterizingPreviewFrames", progress.CompletedItems, progress.TotalItems),
                PreviewExportProgressStage.WritingFrames => F("Transfer.WritingPreviewFrames", progress.CompletedItems, progress.TotalItems),
                PreviewExportProgressStage.EncodingImage => T("Transfer.EncodingPreviewImage"),
                _ => F("Transfer.RenderingPreviewFrames", progress.TotalItems)
            };

            UpdateProjectTransferProgress(new ProjectTransferProgress
            {
                Operation = T("Transfer.ExportingPreview"),
                Message = message,
                CompletedItems = progress.CompletedItems,
                TotalItems = progress.TotalItems
            });
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
            await RefreshAvailableProjectsAsync();
            IsOpenProjectDialogOpen = true;
            StatusText = HasAvailableProjects
                ? F("Status.FoundProjects", AvailableProjects.Count)
                : T("Status.NoInternalProjectsFound");
        }

        [RelayCommand]
        private void ShowRenameProjectDialog(ProjectListItemViewModel projectItem)
        {
            projectItem ??= SelectedAvailableProject;
            if (projectItem is null || string.IsNullOrWhiteSpace(projectItem.ProjectPath))
            {
                StatusText = T("Status.NoProjectSelected");
                return;
            }

            _projectBeingRenamed = projectItem;
            RenameProjectName = projectItem.Name;
            IsRenameProjectDialogOpen = true;
            StatusText = F("Status.RenamingProject", projectItem.Name);
        }

        [RelayCommand]
        private async Task RenameProjectAsync()
        {
            ProjectListItemViewModel projectItem = _projectBeingRenamed;
            if (projectItem is null || string.IsNullOrWhiteSpace(projectItem.ProjectPath))
            {
                StatusText = T("Status.NoProjectSelected");
                return;
            }

            string projectName = string.IsNullOrWhiteSpace(RenameProjectName)
                ? T("Dialog.UntitledEffectProject")
                : RenameProjectName.Trim();
            bool isCurrentProject = SameProjectPath(CurrentProject?.RootPath, projectItem.ProjectPath);
            bool wasOpenProjectDialogOpen = IsOpenProjectDialogOpen;

            IsRenameProjectDialogOpen = false;
            if (isCurrentProject)
            {
                IsOpenProjectDialogOpen = false;
            }

            if (isCurrentProject && !await ConfirmAllUnsavedChangesAsync())
            {
                StatusText = T("Status.CanceledRenamingProject");
                IsOpenProjectDialogOpen = wasOpenProjectDialogOpen;
                IsRenameProjectDialogOpen = true;
                return;
            }

            try
            {
                EffectProject renamedProject = await _projectService.RenameProjectAsync(projectItem.ProjectPath, projectName);
                await RefreshAvailableProjectsAsync(renamedProject.RootPath);
                if (isCurrentProject)
                {
                    LoadProject(renamedProject);
                }

                IsOpenProjectDialogOpen = wasOpenProjectDialogOpen;
                _projectBeingRenamed = null;
                StatusText = F("Status.RenamedProject", renamedProject.Manifest.Name);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                StatusText = F("Status.CouldNotRenameProject", ex.Message);
                IsOpenProjectDialogOpen = wasOpenProjectDialogOpen;
                IsRenameProjectDialogOpen = true;
            }
        }

        [RelayCommand]
        private void CancelRenameProject()
        {
            IsRenameProjectDialogOpen = false;
            _projectBeingRenamed = null;
            StatusText = T("Status.CanceledRenamingProject");
        }

        [RelayCommand]
        private void ShowDeleteProjectDialog(ProjectListItemViewModel projectItem)
        {
            projectItem ??= SelectedAvailableProject;
            if (projectItem is null || string.IsNullOrWhiteSpace(projectItem.ProjectPath))
            {
                StatusText = T("Status.NoProjectSelected");
                return;
            }

            _projectBeingDeleted = projectItem;
            DeleteProjectMessage = F("Dialog.DeleteProjectMessage", projectItem.Name, projectItem.DirectoryName);
            IsDeleteProjectDialogOpen = true;
            StatusText = F("Status.DeletingProject", projectItem.Name);
        }

        [RelayCommand]
        private async Task DeleteProjectAsync()
        {
            ProjectListItemViewModel projectItem = _projectBeingDeleted;
            if (projectItem is null || string.IsNullOrWhiteSpace(projectItem.ProjectPath))
            {
                StatusText = T("Status.NoProjectSelected");
                return;
            }

            string deletedName = projectItem.Name;
            bool isCurrentProject = SameProjectPath(CurrentProject?.RootPath, projectItem.ProjectPath);
            IsDeleteProjectDialogOpen = false;

            try
            {
                await _projectService.DeleteProjectAsync(projectItem.ProjectPath);
                _projectBeingDeleted = null;

                if (isCurrentProject)
                {
                    CurrentProject = null;
                    _effectWorld.LoadProject(null);
                    _luaHost = new LuaHost(_effectWorld);
                    ShowWelcomePage();
                }

                await RefreshAvailableProjectsAsync();
                StatusText = F("Status.DeletedProject", deletedName);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                StatusText = F("Status.CouldNotDeleteProject", ex.Message);
                IsDeleteProjectDialogOpen = true;
            }
        }

        [RelayCommand]
        private void CancelDeleteProject()
        {
            IsDeleteProjectDialogOpen = false;
            _projectBeingDeleted = null;
            StatusText = T("Status.CanceledDeletingProject");
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

        private async Task RefreshAvailableProjectsAsync(string selectedProjectPath = null)
        {
            AvailableProjects.Clear();
            SelectedAvailableProject = null;

            IReadOnlyList<ProjectInfo> projects = await _projectService.ListProjectsAsync();
            foreach (ProjectInfo project in projects)
            {
                ProjectListItemViewModel item = new(project);
                AvailableProjects.Add(item);
                if (!string.IsNullOrWhiteSpace(selectedProjectPath) &&
                    SameProjectPath(item.ProjectPath, selectedProjectPath))
                {
                    SelectedAvailableProject = item;
                }
            }

            OnPropertyChanged(nameof(HasAvailableProjects));
            OpenProjectMessage = HasAvailableProjects
                ? T("Status.OpenProjectMessageHasProjects")
                : T("Status.OpenProjectMessageEmpty");
        }

        private static bool SameProjectPath(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private void LoadProject(EffectProject project)
        {
            CancelPendingProjectTreeSearchRefresh();
            _appliedProjectTreeSearchText = NormalizeProjectTreeSearchText(ProjectTreeSearchText);
            CurrentProject = project;
            _effectWorld.LoadProject(CurrentProject);
            _luaHost = new LuaHost(_effectWorld);
            SelectedProjectItem = null;
            _projectTreeExpansionState.Clear();
            ClearRecentlyOpenedResources();
            RebuildProjectTree();
            CloseAllEditors();
        }

        private void ShowWelcomePage()
        {
            CancelPendingProjectTreeSearchRefresh();
            _appliedProjectTreeSearchText = NormalizeProjectTreeSearchText(ProjectTreeSearchText);
            ProjectItems.Clear();
            SelectedProjectItem = null;
            ProjectTreeResourceCount = 0;
            _projectTreeExpansionState.Clear();
            ClearRecentlyOpenedResources();
            NotifyProjectTreeProperties();
            CloseAllEditors();
            OpenEditorTab(CreateWelcomeEditor());
        }

        private WelcomeEditorViewModel CreateWelcomeEditor()
        {
            return new WelcomeEditorViewModel(
                ShowNewProjectDialogCommand,
                ShowOpenProjectDialogCommand,
                RequestImportResourceFolderCommand);
        }

        private void RefreshCurrentProject()
        {
            RefreshCurrentProjectState();
            SelectedProjectItem = null;
            RebuildProjectTree();
        }

        private void RefreshCurrentProjectState()
        {
            CurrentProject?.RebuildAssetIndex();
            _effectWorld.LoadProject(CurrentProject);
            _luaHost = new LuaHost(_effectWorld);
            OnPropertyChanged(nameof(CurrentProject));
        }

        partial void OnSelectedProjectItemChanged(ProjectExplorerItemViewModel value)
        {
            OnPropertyChanged(nameof(CanDeleteSelectedResource));

            if (value is null || !value.IsSelectable)
            {
                return;
            }

            OpenEditor(value);
        }

        private void OpenEditor(ProjectExplorerItemViewModel item)
        {
            AddRecentlyOpenedResource(item);

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
                AddRecentlyOpenedResource(kind, image.Id, image.Path);
                OpenOrSelectEditor(kind, assetId, () => new ImageEditorViewModel(image, CurrentProject));
                return;
            }

            if (kind == EffectAssetKind.Showcase &&
                CurrentProject.Assets.Showcases.TryGetValue(assetId, out ShowcaseAsset showcase))
            {
                AddRecentlyOpenedResource(kind, showcase.Id, showcase.Path);
                OpenOrSelectEditor(kind, assetId, () => new ShowcaseEditorViewModel(showcase, _luaHost, CurrentProject));
                return;
            }

            if (asset is not null)
            {
                AddRecentlyOpenedResource(kind, asset.Id, asset.Path);
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
        private void SelectPreviousEditor()
        {
            if (OpenEditors.Count <= 1)
            {
                return;
            }

            int index = OpenEditors.IndexOf(SelectedEditor);
            int previousIndex = index <= 0 ? OpenEditors.Count - 1 : index - 1;
            SelectedEditor = OpenEditors[previousIndex];
        }

        [RelayCommand]
        private void SelectNextEditor()
        {
            if (OpenEditors.Count <= 1)
            {
                return;
            }

            int index = OpenEditors.IndexOf(SelectedEditor);
            int nextIndex = index < 0 || index >= OpenEditors.Count - 1 ? 0 : index + 1;
            SelectedEditor = OpenEditors[nextIndex];
        }

        [RelayCommand]
        private void MoveEditorLeft(EditorViewModelBase editor)
        {
            editor ??= SelectedEditor;
            if (!CanMoveEditorLeft(editor))
            {
                return;
            }

            int index = OpenEditors.IndexOf(editor);
            OpenEditors.Move(index, index - 1);
            SelectedEditor = editor;
            NotifyOpenEditorProperties();
            StatusText = F("Status.MovedDocumentLeft", editor.Title);
        }

        [RelayCommand]
        private void MoveEditorRight(EditorViewModelBase editor)
        {
            editor ??= SelectedEditor;
            if (!CanMoveEditorRight(editor))
            {
                return;
            }

            int index = OpenEditors.IndexOf(editor);
            OpenEditors.Move(index, index + 1);
            SelectedEditor = editor;
            NotifyOpenEditorProperties();
            StatusText = F("Status.MovedDocumentRight", editor.Title);
        }

        public bool ReorderEditorTab(EditorViewModelBase source, EditorViewModelBase target, bool insertAfter)
        {
            if (source is null || target is null || ReferenceEquals(source, target))
            {
                return false;
            }

            int sourceIndex = OpenEditors.IndexOf(source);
            int targetIndex = OpenEditors.IndexOf(target);
            if (sourceIndex < 0 || targetIndex < 0)
            {
                return false;
            }

            source.IsPinned = target.IsPinned;
            int insertIndex = targetIndex + (insertAfter ? 1 : 0);
            if (sourceIndex < insertIndex)
            {
                insertIndex--;
            }

            insertIndex = System.Math.Clamp(insertIndex, 0, OpenEditors.Count - 1);
            if (sourceIndex == insertIndex)
            {
                SelectedEditor = source;
                NotifyOpenEditorProperties();
                return false;
            }

            OpenEditors.Move(sourceIndex, insertIndex);
            SelectedEditor = source;
            NotifyOpenEditorProperties();
            StatusText = F("Status.ReorderedDocument", source.Title);
            return true;
        }

        [RelayCommand]
        private void ToggleEditorPinned(EditorViewModelBase editor)
        {
            editor ??= SelectedEditor;
            if (editor is null)
            {
                return;
            }

            editor.IsPinned = !editor.IsPinned;
            MoveEditorToPinnedGroup(editor);
            SelectedEditor = editor;
            NotifyOpenEditorProperties();
            StatusText = editor.IsPinned
                ? F("Status.PinnedDocument", editor.Title)
                : F("Status.UnpinnedDocument", editor.Title);
        }

        [RelayCommand]
        private async Task SaveAllEditorsAsync()
        {
            int savedCount = 0;
            foreach (EditorViewModelBase editor in OpenEditors.Where(editor => editor.IsDirty).ToList())
            {
                if (!await SaveEditorAsync(editor))
                {
                    StatusText = F("Status.CanceledSavingDocuments", savedCount);
                    return;
                }

                savedCount++;
            }

            StatusText = savedCount == 0
                ? T("Status.NoUnsavedDocuments")
                : F("Status.SavedDocuments", savedCount);
        }

        [RelayCommand]
        private async Task CloseCurrentEditorAsync()
        {
            await CloseEditorAsync(SelectedEditor);
        }

        [RelayCommand]
        private async Task CloseEditorAsync(EditorViewModelBase editor)
        {
            if (editor is null)
            {
                return;
            }

            if (!editor.CanClose)
            {
                StatusText = F("Status.DocumentCannotClose", editor.Title);
                return;
            }

            if (!await ConfirmUnsavedChangesAsync(editor))
            {
                StatusText = F("Status.CanceledClosingDocument", editor.Title);
                return;
            }

            CloseEditorCore(editor);
        }

        [RelayCommand]
        private async Task CloseOtherEditorsAsync(EditorViewModelBase editor)
        {
            editor ??= SelectedEditor;
            if (editor is null)
            {
                return;
            }

            SelectedEditor = editor;
            IReadOnlyList<EditorViewModelBase> targets = OpenEditors
                .Where(candidate => !ReferenceEquals(candidate, editor) && candidate.CanClose)
                .ToList();

            await CloseEditorSetAsync(targets, "Status.ClosedOtherDocuments");
        }

        [RelayCommand]
        private async Task CloseEditorsToLeftAsync(EditorViewModelBase editor)
        {
            editor ??= SelectedEditor;
            if (editor is null)
            {
                return;
            }

            int index = OpenEditors.IndexOf(editor);
            if (index <= 0)
            {
                return;
            }

            SelectedEditor = editor;
            IReadOnlyList<EditorViewModelBase> targets = OpenEditors
                .Take(index)
                .Where(candidate => candidate.CanClose)
                .ToList();

            await CloseEditorSetAsync(targets, "Status.ClosedDocuments");
        }

        [RelayCommand]
        private async Task CloseEditorsToRightAsync(EditorViewModelBase editor)
        {
            editor ??= SelectedEditor;
            if (editor is null)
            {
                return;
            }

            int index = OpenEditors.IndexOf(editor);
            if (index < 0 || index >= OpenEditors.Count - 1)
            {
                return;
            }

            SelectedEditor = editor;
            IReadOnlyList<EditorViewModelBase> targets = OpenEditors
                .Skip(index + 1)
                .Where(candidate => candidate.CanClose)
                .ToList();

            await CloseEditorSetAsync(targets, "Status.ClosedDocuments");
        }

        [RelayCommand]
        private void CloseSavedEditors()
        {
            IReadOnlyList<EditorViewModelBase> targets = OpenEditors
                .Where(editor => editor.CanClose && !editor.IsDirty)
                .ToList();

            int closedCount = CloseEditorSetWithoutPrompt(targets);
            StatusText = closedCount == 0
                ? T("Status.NoSavedDocumentsToClose")
                : F("Status.ClosedDocuments", closedCount);
        }

        [RelayCommand]
        private async Task CloseAllEditorTabsAsync()
        {
            IReadOnlyList<EditorViewModelBase> targets = OpenEditors
                .Where(editor => editor.CanClose)
                .ToList();

            await CloseEditorSetAsync(targets, "Status.ClosedDocuments");
        }

        private async Task CloseEditorSetAsync(IReadOnlyList<EditorViewModelBase> editors, string completedStatusKey)
        {
            int closedCount = 0;
            foreach (EditorViewModelBase editor in editors)
            {
                if (!OpenEditors.Contains(editor) || !editor.CanClose)
                {
                    continue;
                }

                if (!await ConfirmUnsavedChangesAsync(editor))
                {
                    StatusText = F("Status.CanceledClosingDocument", editor.Title);
                    return;
                }

                CloseEditorCore(editor);
                closedCount++;
            }

            StatusText = closedCount == 0
                ? T("Status.NoDocumentsClosed")
                : F(completedStatusKey, closedCount);
        }

        private int CloseEditorSetWithoutPrompt(IReadOnlyList<EditorViewModelBase> editors)
        {
            int closedCount = 0;
            foreach (EditorViewModelBase editor in editors)
            {
                if (!OpenEditors.Contains(editor) || !editor.CanClose)
                {
                    continue;
                }

                CloseEditorCore(editor);
                closedCount++;
            }

            return closedCount;
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

        private bool CanMoveEditorLeft(EditorViewModelBase editor)
        {
            int index = OpenEditors.IndexOf(editor);
            return index > 0 && OpenEditors[index - 1].IsPinned == editor.IsPinned;
        }

        private bool CanMoveEditorRight(EditorViewModelBase editor)
        {
            int index = OpenEditors.IndexOf(editor);
            return index >= 0 &&
                index < OpenEditors.Count - 1 &&
                OpenEditors[index + 1].IsPinned == editor.IsPinned;
        }

        private void MoveEditorToPinnedGroup(EditorViewModelBase editor)
        {
            int index = OpenEditors.IndexOf(editor);
            if (index < 0)
            {
                return;
            }

            int targetIndex = editor.IsPinned
                ? OpenEditors.TakeWhile(candidate => candidate.IsPinned && !ReferenceEquals(candidate, editor)).Count()
                : OpenEditors.Count(candidate => candidate.IsPinned);
            targetIndex = System.Math.Clamp(targetIndex, 0, OpenEditors.Count - 1);

            if (index != targetIndex)
            {
                OpenEditors.Move(index, targetIndex);
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

        private EditorViewModelBase FindOpenEditor(EffectAssetKind kind, string assetId)
        {
            string documentId = EditorViewModelBase.CreateDocumentId(kind, assetId);
            return OpenEditors.FirstOrDefault(editor => editor.DocumentId == documentId);
        }

        private void CloseResourceEditors(EffectAssetKind kind, string assetId)
        {
            string documentId = EditorViewModelBase.CreateDocumentId(kind, assetId);
            IReadOnlyList<EditorViewModelBase> targets = OpenEditors
                .Where(editor => editor.DocumentId == documentId)
                .ToList();

            CloseEditorSetWithoutPrompt(targets);
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
            OnPropertyChanged(nameof(CanExportSelectedPreview));
            NotifyOpenEditorProperties();
            RefreshPreviewExportTimelineOptions();

            if (value is not null)
            {
                StatusText = value.IsDirty
                    ? F("Status.EditingUnsaved", value.Title)
                    : F("Status.Editing", value.Title);
            }
        }

        private void OnOpenEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is not EditorViewModelBase editor)
            {
                return;
            }

            if (e.PropertyName == nameof(EditorViewModelBase.IsDirty))
            {
                NotifyOpenEditorProperties();

                if (ReferenceEquals(editor, SelectedEditor))
                {
                    StatusText = editor.IsDirty
                        ? F("Status.EditingUnsaved", editor.Title)
                        : F("Status.Editing", editor.Title);
                }
            }
            else if (e.PropertyName is nameof(EditorViewModelBase.IsPinned) or nameof(EditorViewModelBase.Title))
            {
                NotifyOpenEditorProperties();
            }
            else if (ReferenceEquals(editor, SelectedEditor) &&
                editor is EffectEditorViewModel &&
                e.PropertyName is nameof(EffectEditorViewModel.ReanimTimelineRevision)
                    or nameof(EffectEditorViewModel.SelectedReanimLayer)
                    or nameof(EffectEditorViewModel.ReanimFps))
            {
                RefreshPreviewExportTimelineOptions();
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

        private void OnOpenEditorsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            NotifyOpenEditorProperties();
        }

        private void NotifyOpenEditorProperties()
        {
            OnPropertyChanged(nameof(HasOpenEditors));
            OnPropertyChanged(nameof(HasMultipleOpenEditors));
            OnPropertyChanged(nameof(HasClosableEditors));
            OnPropertyChanged(nameof(CanCloseSelectedEditor));
            OnPropertyChanged(nameof(CanCloseOtherEditors));
            OnPropertyChanged(nameof(CanSelectPreviousEditor));
            OnPropertyChanged(nameof(CanSelectNextEditor));
            OnPropertyChanged(nameof(CanMoveSelectedEditorLeft));
            OnPropertyChanged(nameof(CanMoveSelectedEditorRight));
            OnPropertyChanged(nameof(CanSaveAnyEditor));
            OnPropertyChanged(nameof(CanCloseSavedEditors));
            OnPropertyChanged(nameof(CanExportSelectedPreview));
        }

        private void UpdateProjectExplorerItemIdentity(
            EffectAssetKind kind,
            string oldAssetId,
            string newAssetId,
            string path)
        {
            UpdateRecentlyOpenedResourceIdentity(kind, oldAssetId, newAssetId, path);
            RebuildProjectTree();
            ProjectExplorerItemViewModel item = FindProjectExplorerItem(ProjectItems, kind, newAssetId, path);
            if (item is not null)
            {
                SelectedProjectItem = item;
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

        private ProjectExplorerItemViewModel AddOrUpdateProjectExplorerItem(
            EffectAssetKind kind,
            string assetId,
            string path)
        {
            RebuildProjectTree();
            return FindProjectExplorerItem(ProjectItems, kind, assetId, path);
        }

        private static void InsertProjectExplorerItemSorted(
            ObservableCollection<ProjectExplorerItemViewModel> siblings,
            ProjectExplorerItemViewModel item)
        {
            int index = 0;
            while (index < siblings.Count && CompareProjectExplorerItems(siblings[index], item) <= 0)
            {
                index++;
            }

            siblings.Insert(index, item);
        }

        private static int CompareProjectExplorerItems(ProjectExplorerItemViewModel left, ProjectExplorerItemViewModel right)
        {
            int titleComparison = ProjectTreeNameComparer.Compare(left.Title, right.Title);
            if (titleComparison != 0)
            {
                return titleComparison;
            }

            int assetIdComparison = ProjectTreeNameComparer.Compare(left.AssetId, right.AssetId);
            return assetIdComparison != 0
                ? assetIdComparison
                : ProjectTreeNameComparer.Compare(left.Path, right.Path);
        }

        private void RebuildProjectTree()
        {
            CaptureProjectTreeExpansionState();
            ProjectItems.Clear();

            if (CurrentProject is null)
            {
                ProjectTreeResourceCount = 0;
                NotifyProjectTreeProperties();
                return;
            }

            ProjectTreeResourceCount =
                CurrentProject.Manifest.Images.Count +
                CurrentProject.Manifest.Reanims.Count +
                CurrentProject.Manifest.Particles.Count +
                CurrentProject.Manifest.Trails.Count +
                CurrentProject.Manifest.Showcases.Count;

            bool isSearchActive = !string.IsNullOrWhiteSpace(_appliedProjectTreeSearchText);
            int visibleCount = 0;
            ProjectExplorerItemViewModel root = new(
                CurrentProject.Manifest.Name,
                EffectAssetKind.Project)
            {
                IsExpanded = GetProjectTreeExpansionState(EffectAssetKind.Project, string.Empty, defaultValue: true)
            };

            visibleCount += AddProjectTreeFolder(
                root,
                ImagesProjectTreeGroupKey,
                T("ProjectTree.Images"),
                CreateFilteredResourceItems(CurrentProject.Manifest.Images, EffectAssetKind.Image, asset => asset.Id, asset => asset.Path),
                isSearchActive);

            visibleCount += AddProjectTreeFolder(
                root,
                ReanimsProjectTreeGroupKey,
                T("ProjectTree.Reanim"),
                CreateFilteredResourceItems(CurrentProject.Manifest.Reanims, EffectAssetKind.Reanim, asset => asset.Id, asset => asset.Path),
                isSearchActive);

            visibleCount += AddProjectTreeFolder(
                root,
                ParticlesProjectTreeGroupKey,
                T("ProjectTree.Particles"),
                CreateFilteredResourceItems(CurrentProject.Manifest.Particles, EffectAssetKind.Particle, asset => asset.Id, asset => asset.Path),
                isSearchActive);

            visibleCount += AddProjectTreeFolder(
                root,
                TrailsProjectTreeGroupKey,
                T("ProjectTree.Trails"),
                CreateFilteredResourceItems(CurrentProject.Manifest.Trails, EffectAssetKind.Trail, asset => asset.Id, asset => asset.Path),
                isSearchActive);

            visibleCount += AddProjectTreeFolder(
                root,
                ShowcasesProjectTreeGroupKey,
                T("ProjectTree.Showcases"),
                CreateFilteredResourceItems(CurrentProject.Manifest.Showcases, EffectAssetKind.Showcase, asset => asset.Id, asset => asset.Path),
                isSearchActive);

            if (!isSearchActive || root.Children.Count > 0)
            {
                ProjectItems.Add(root);
            }

            if (SelectedProjectItem is not null &&
                FindProjectExplorerItem(ProjectItems, SelectedProjectItem.Kind, SelectedProjectItem.AssetId, SelectedProjectItem.Path) is null)
            {
                SelectedProjectItem = null;
            }

            NotifyProjectTreeProperties();
        }

        private int AddProjectTreeFolder(
            ProjectExplorerItemViewModel root,
            string groupKey,
            string title,
            IEnumerable<ProjectExplorerItemViewModel> children,
            bool isSearchActive)
        {
            List<ProjectExplorerItemViewModel> childItems = children.ToList();
            if (isSearchActive && childItems.Count == 0)
            {
                return 0;
            }

            root.Children.Add(CreateFolder(groupKey, title, childItems));
            return childItems.Count;
        }

        private ProjectExplorerItemViewModel CreateFolder(
            string groupKey,
            string title,
            IReadOnlyList<ProjectExplorerItemViewModel> children)
        {
            ProjectExplorerItemViewModel folder = new(
                title,
                EffectAssetKind.Folder,
                groupKey)
            {
                IsExpanded = GetProjectTreeExpansionState(EffectAssetKind.Folder, groupKey, defaultValue: true)
            };

            foreach (ProjectExplorerItemViewModel child in children)
            {
                InsertProjectExplorerItemSorted(folder.Children, child);
            }

            return folder;
        }

        private IEnumerable<ProjectExplorerItemViewModel> CreateFilteredResourceItems<T>(
            IEnumerable<T> assets,
            EffectAssetKind kind,
            Func<T, string> idSelector,
            Func<T, string> pathSelector)
        {
            foreach (T asset in assets)
            {
                string assetId = idSelector(asset) ?? string.Empty;
                string path = pathSelector(asset) ?? string.Empty;
                if (ProjectTreeResourceMatches(kind, assetId, path))
                {
                    yield return CreateResourceProjectTreeItem(kind, assetId, path);
                }
            }
        }

        private ProjectExplorerItemViewModel CreateResourceProjectTreeItem(
            EffectAssetKind kind,
            string assetId,
            string path)
        {
            return new ProjectExplorerItemViewModel(
                assetId,
                kind,
                assetId,
                path);
        }

        private bool ProjectTreeResourceMatches(EffectAssetKind kind, string assetId, string path)
        {
            string query = _appliedProjectTreeSearchText;
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            string kindName = Loc.Text($"DocumentKind.{kind}");
            string[] terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return terms.All(term =>
                ContainsSearchTerm(assetId, term) ||
                ContainsSearchTerm(kind.ToString(), term) ||
                ContainsSearchTerm(kindName, term));
        }

        private static bool ContainsSearchTerm(string value, string term)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                value.Contains(term, StringComparison.OrdinalIgnoreCase);
        }

        private void ScheduleProjectTreeSearchRefresh(string searchText)
        {
            CancelPendingProjectTreeSearchRefresh();

            string normalizedSearchText = NormalizeProjectTreeSearchText(searchText);
            if (string.IsNullOrWhiteSpace(normalizedSearchText))
            {
                ApplyProjectTreeSearchText(normalizedSearchText);
                return;
            }

            CancellationTokenSource cancellation = new();
            _projectTreeSearchRefreshCancellation = cancellation;
            _ = ApplyProjectTreeSearchTextAfterDelayAsync(normalizedSearchText, cancellation);
        }

        private async Task ApplyProjectTreeSearchTextAfterDelayAsync(
            string searchText,
            CancellationTokenSource cancellation)
        {
            try
            {
                await Task.Delay(ProjectTreeSearchDebounceMilliseconds, cancellation.Token);
                if (!cancellation.IsCancellationRequested)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (!cancellation.IsCancellationRequested)
                        {
                            ApplyProjectTreeSearchText(searchText);
                        }
                    });
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (ReferenceEquals(_projectTreeSearchRefreshCancellation, cancellation))
                {
                    _projectTreeSearchRefreshCancellation = null;
                }

                cancellation.Dispose();
            }
        }

        private void ApplyProjectTreeSearchText(string searchText)
        {
            string normalizedSearchText = NormalizeProjectTreeSearchText(searchText);
            if (string.Equals(_appliedProjectTreeSearchText, normalizedSearchText, StringComparison.Ordinal))
            {
                return;
            }

            _appliedProjectTreeSearchText = normalizedSearchText;
            RebuildProjectTree();
        }

        private void CancelPendingProjectTreeSearchRefresh()
        {
            CancellationTokenSource cancellation = _projectTreeSearchRefreshCancellation;
            _projectTreeSearchRefreshCancellation = null;
            cancellation?.Cancel();
        }

        private static string NormalizeProjectTreeSearchText(string searchText)
        {
            return string.IsNullOrWhiteSpace(searchText) ? string.Empty : searchText.Trim();
        }

        private void CaptureProjectTreeExpansionState()
        {
            foreach (ProjectExplorerItemViewModel item in ProjectItems)
            {
                CaptureProjectTreeExpansionState(item);
            }
        }

        private void CaptureProjectTreeExpansionState(ProjectExplorerItemViewModel item)
        {
            if (item is null)
            {
                return;
            }

            if (item.Children.Count > 0)
            {
                _projectTreeExpansionState[GetProjectTreeExpansionKey(item.Kind, item.AssetId)] = item.IsExpanded;
            }

            foreach (ProjectExplorerItemViewModel child in item.Children)
            {
                CaptureProjectTreeExpansionState(child);
            }
        }

        private bool GetProjectTreeExpansionState(EffectAssetKind kind, string assetId, bool defaultValue)
        {
            return _projectTreeExpansionState.TryGetValue(GetProjectTreeExpansionKey(kind, assetId), out bool isExpanded)
                ? isExpanded
                : defaultValue;
        }

        private static string GetProjectTreeExpansionKey(EffectAssetKind kind, string assetId)
        {
            return $"{kind}:{assetId ?? string.Empty}";
        }

        private void AddRecentlyOpenedResource(ProjectExplorerItemViewModel item)
        {
            if (item?.IsSelectable != true)
            {
                return;
            }

            AddRecentlyOpenedResource(item.Kind, item.AssetId, item.Path);
        }

        private void AddRecentlyOpenedResource(EffectAssetKind kind, string requestedAssetId, string requestedPath)
        {
            if (CurrentProject is null)
            {
                return;
            }

            if (!TryResolveProjectResource(kind, requestedAssetId, requestedPath, out string assetId, out string path))
            {
                assetId = requestedAssetId ?? string.Empty;
                path = requestedPath ?? string.Empty;
            }

            RemoveRecentlyOpenedResourceIdentity(kind, assetId, path);
            _recentlyOpenedResources.Insert(0, new ProjectExplorerResourceIdentity(kind, assetId, path));
            while (_recentlyOpenedResources.Count > MaxRecentlyOpenedProjectItems)
            {
                _recentlyOpenedResources.RemoveAt(_recentlyOpenedResources.Count - 1);
            }

            RebuildRecentlyOpenedProjectItems();
        }

        private void RemoveRecentlyOpenedResource(EffectAssetKind kind, string assetId, string path)
        {
            if (RemoveRecentlyOpenedResourceIdentity(kind, assetId, path))
            {
                RebuildRecentlyOpenedProjectItems();
            }
        }

        private bool RemoveRecentlyOpenedResourceIdentity(EffectAssetKind kind, string assetId, string path)
        {
            int removedCount = _recentlyOpenedResources.RemoveAll(identity =>
                identity.Kind == kind &&
                ResourceIdentityMatches(identity.AssetId, identity.Path, assetId, path));
            return removedCount > 0;
        }

        private void UpdateRecentlyOpenedResourceIdentity(
            EffectAssetKind kind,
            string oldAssetId,
            string newAssetId,
            string path)
        {
            bool changed = false;
            foreach (ProjectExplorerResourceIdentity identity in _recentlyOpenedResources)
            {
                if (identity.Kind == kind && ResourceIdentityMatches(identity.AssetId, identity.Path, oldAssetId, path))
                {
                    identity.AssetId = newAssetId;
                    identity.Path = path;
                    changed = true;
                }
            }

            if (changed)
            {
                RebuildRecentlyOpenedProjectItems();
            }
        }

        private void RebuildRecentlyOpenedProjectItems()
        {
            RecentlyOpenedProjectItems.Clear();

            if (CurrentProject is null)
            {
                NotifyProjectTreeProperties();
                return;
            }

            foreach (ProjectExplorerResourceIdentity identity in _recentlyOpenedResources.ToList())
            {
                if (!TryResolveProjectResource(identity.Kind, identity.AssetId, identity.Path, out string assetId, out string path))
                {
                    _recentlyOpenedResources.Remove(identity);
                    continue;
                }

                RecentlyOpenedProjectItems.Add(CreateResourceProjectTreeItem(identity.Kind, assetId, path));
            }

            NotifyProjectTreeProperties();
        }

        private void ClearRecentlyOpenedResources()
        {
            _recentlyOpenedResources.Clear();
            RecentlyOpenedProjectItems.Clear();
            NotifyProjectTreeProperties();
        }

        private bool TryResolveProjectResource(
            EffectAssetKind kind,
            string assetId,
            string path,
            out string resolvedAssetId,
            out string resolvedPath)
        {
            resolvedAssetId = string.Empty;
            resolvedPath = string.Empty;
            if (CurrentProject is null)
            {
                return false;
            }

            switch (kind)
            {
                case EffectAssetKind.Image:
                    ImageAsset image = CurrentProject.Manifest.Images.FirstOrDefault(asset =>
                        ResourceIdentityMatches(asset.Id, asset.Path, assetId, path));
                    if (image is not null)
                    {
                        resolvedAssetId = image.Id;
                        resolvedPath = image.Path;
                        return true;
                    }

                    break;

                case EffectAssetKind.Reanim:
                    ReanimAsset reanim = CurrentProject.Manifest.Reanims.FirstOrDefault(asset =>
                        ResourceIdentityMatches(asset.Id, asset.Path, assetId, path));
                    if (reanim is not null)
                    {
                        resolvedAssetId = reanim.Id;
                        resolvedPath = reanim.Path;
                        return true;
                    }

                    break;

                case EffectAssetKind.Particle:
                    EffectAsset particle = CurrentProject.Manifest.Particles.FirstOrDefault(asset =>
                        ResourceIdentityMatches(asset.Id, asset.Path, assetId, path));
                    if (particle is not null)
                    {
                        resolvedAssetId = particle.Id;
                        resolvedPath = particle.Path;
                        return true;
                    }

                    break;

                case EffectAssetKind.Trail:
                    EffectAsset trail = CurrentProject.Manifest.Trails.FirstOrDefault(asset =>
                        ResourceIdentityMatches(asset.Id, asset.Path, assetId, path));
                    if (trail is not null)
                    {
                        resolvedAssetId = trail.Id;
                        resolvedPath = trail.Path;
                        return true;
                    }

                    break;

                case EffectAssetKind.Showcase:
                    ShowcaseAsset showcase = CurrentProject.Manifest.Showcases.FirstOrDefault(asset =>
                        ResourceIdentityMatches(asset.Id, asset.Path, assetId, path));
                    if (showcase is not null)
                    {
                        resolvedAssetId = showcase.Id;
                        resolvedPath = showcase.Path;
                        return true;
                    }

                    break;
            }

            return false;
        }

        private static bool ResourceIdentityMatches(string id, string path, string assetId, string projectPath)
        {
            return (!string.IsNullOrWhiteSpace(assetId) &&
                    string.Equals(id, assetId, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(projectPath) &&
                    string.Equals(path, projectPath, StringComparison.OrdinalIgnoreCase));
        }

        private void NotifyProjectTreeProperties()
        {
            OnPropertyChanged(nameof(HasProjectTreeSearchText));
            OnPropertyChanged(nameof(HasVisibleProjectTreeItems));
            OnPropertyChanged(nameof(HasNoVisibleProjectTreeItems));
            OnPropertyChanged(nameof(HasRecentlyOpenedProjectItems));
            OnPropertyChanged(nameof(ProjectTreeEmptyMessage));
        }

        private bool CanDeleteResourceItem(ProjectExplorerItemViewModel item)
        {
            return CanModifyCurrentProject && item?.IsSelectable == true;
        }

        private sealed class ProjectExplorerResourceIdentity
        {
            public ProjectExplorerResourceIdentity(EffectAssetKind kind, string assetId, string path)
            {
                Kind = kind;
                AssetId = assetId ?? string.Empty;
                Path = path ?? string.Empty;
            }

            public EffectAssetKind Kind { get; }
            public string AssetId { get; set; }
            public string Path { get; set; }
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
