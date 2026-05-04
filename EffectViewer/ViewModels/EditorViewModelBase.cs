using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EffectViewer.Localization;
using EffectViewer.Projects;
using EffectViewer.Rendering;
using EffectViewer.Rendering.TextureUpload;

namespace EffectViewer.ViewModels
{
    public abstract partial class EditorViewModelBase : ViewModelBase, IDisposable
    {
        private RenderFrame _previewFrame;
        private IRenderFrameProvider _previewFrameProvider;
        private ITextureSource _textureSource;
        private bool _isDirty;
        private bool _isPinned;
        private bool _isSidePanelOnLeft;
        private bool _isSidePanelVisible = true;

        private string _title;
        private string _documentId;

        public string Title
        {
            get => _title;
            protected set
            {
                string newTitle = value ?? string.Empty;
                if (SetProperty(ref _title, newTitle))
                {
                    OnPropertyChanged(nameof(TabTitle));
                    OnPropertyChanged(nameof(TabToolTip));
                }
            }
        }
        public string TabTitle => IsDirty ? $"{Title}*" : Title;
        public string KindCode => Kind switch
        {
            EffectAssetKind.Image => "IMG",
            EffectAssetKind.Font => "FNT",
            EffectAssetKind.Reanim => "REA",
            EffectAssetKind.Particle => "PAR",
            EffectAssetKind.Trail => "TRL",
            EffectAssetKind.Showcase => "LUA",
            EffectAssetKind.Project => "APP",
            _ => Kind.ToString().ToUpperInvariant()
        };
        public string KindDisplayName => LocalizationManager.Instance.Text($"DocumentKind.{Kind}");
        public string TabToolTip => IsPinned
            ? LocalizationManager.Instance.Format("Document.TabToolTipPinned", Title, KindDisplayName)
            : LocalizationManager.Instance.Format("Document.TabToolTip", Title, KindDisplayName);
        public string DocumentId
        {
            get => _documentId;
            protected set => SetProperty(ref _documentId, value ?? string.Empty);
        }
        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            internal set => SetProperty(ref _isSelected, value);
        }
        public EffectAssetKind Kind { get; }
        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (SetProperty(ref _isDirty, value))
                {
                    OnPropertyChanged(nameof(TabTitle));
                    OnPropertyChanged(nameof(TabToolTip));
                }
            }
        }

        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                if (SetProperty(ref _isPinned, value))
                {
                    OnPropertyChanged(nameof(TabToolTip));
                }
            }
        }

        public virtual bool CanClose => true;
        public virtual bool SupportsSave => false;
        public virtual bool SavesWithProjectManifest => false;
        public virtual bool SupportsFileExport => false;
        public virtual string ExportPath => string.Empty;
        public virtual int PreviewExportDefaultFps => 30;
        public virtual bool CanUndo => false;
        public virtual bool CanRedo => false;

        public bool IsSidePanelOnLeft
        {
            get => _isSidePanelOnLeft;
            set
            {
                if (SetProperty(ref _isSidePanelOnLeft, value))
                {
                    OnPropertyChanged(nameof(IsSidePanelOnRight));
                }
            }
        }

        public bool IsSidePanelOnRight => !IsSidePanelOnLeft;

        public bool IsSidePanelVisible
        {
            get => _isSidePanelVisible;
            set => SetProperty(ref _isSidePanelVisible, value);
        }

        [ObservableProperty]
        private int _layoutResetRevision;

        [ObservableProperty]
        private bool _isTabDragging;

        [ObservableProperty]
        private bool _isTabDropBefore;

        [ObservableProperty]
        private bool _isTabDropAfter;

        public RenderFrame PreviewFrame
        {
            get => _previewFrame;
            protected set => SetProperty(ref _previewFrame, value);
        }

        public IRenderFrameProvider PreviewFrameProvider
        {
            get => _previewFrameProvider;
            protected set => SetProperty(ref _previewFrameProvider, value);
        }

        public ITextureSource TextureSource
        {
            get => _textureSource;
            protected set => SetProperty(ref _textureSource, value);
        }

        protected EditorViewModelBase(string title, EffectAssetKind kind)
        {
            Kind = kind;
            Title = title;
            DocumentId = CreateDocumentId(kind, title);
            PreviewFrame = EffectPreviewFrameBuilder.BuildPlaceholder(kind, title);
            TextureSource = new GeneratedTextureSource();
            LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
        }

        public static string CreateDocumentId(EffectAssetKind kind, string title)
        {
            return $"{kind}:{title}";
        }

        public virtual Task SaveAsync(EffectProjectService projectService, EffectProject project)
        {
            MarkClean();
            return Task.CompletedTask;
        }

        public virtual Task ExportAsync(EffectProjectService projectService, EffectProject project, Stream outputStream, string targetFileName)
        {
            return projectService.ExportProjectFileAsync(project, ExportPath, outputStream);
        }

        public virtual IRenderFrameProvider CreatePreviewExportFrameProvider()
        {
            return null;
        }

        public virtual IReadOnlyList<PreviewExportTimelineOption> GetPreviewExportTimelineOptions()
        {
            return [];
        }

        public virtual string GetDefaultPreviewExportTimelineId()
        {
            return null;
        }

        public virtual IRenderFrameProvider CreatePreviewExportFrameProvider(PreviewExportTimelineOption timeline)
        {
            return CreatePreviewExportFrameProvider();
        }

        protected void MarkDirty()
        {
            IsDirty = true;
        }

        protected void MarkClean()
        {
            IsDirty = false;
        }

        public virtual void AcceptSavedState()
        {
            MarkClean();
        }

        public virtual void DiscardChanges()
        {
            MarkClean();
        }

        public virtual void Undo()
        {
        }

        public virtual void Redo()
        {
        }

        public virtual void Dispose()
        {
            LocalizationManager.Instance.LanguageChanged -= OnLanguageChanged;
            if (PreviewFrameProvider is IDisposable disposableProvider)
            {
                disposableProvider.Dispose();
            }
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(KindDisplayName));
            OnPropertyChanged(nameof(TabToolTip));
        }

        [RelayCommand]
        private void DockSidePanelLeft()
        {
            IsSidePanelOnLeft = true;
            IsSidePanelVisible = true;
        }

        [RelayCommand]
        private void DockSidePanelRight()
        {
            IsSidePanelOnLeft = false;
            IsSidePanelVisible = true;
        }

        [RelayCommand]
        private void ToggleSidePanel()
        {
            IsSidePanelVisible = !IsSidePanelVisible;
        }

        [RelayCommand]
        private void ResetEditorLayout()
        {
            IsSidePanelOnLeft = false;
            IsSidePanelVisible = true;
            LayoutResetRevision++;
        }
    }
}
