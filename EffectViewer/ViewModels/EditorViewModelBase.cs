using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
                }
            }
        }
        public string TabTitle => IsDirty ? $"{Title}*" : Title;
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
                }
            }
        }

        public virtual bool SupportsSave => false;
        public virtual bool SavesWithProjectManifest => false;
        public virtual bool SupportsFileExport => false;
        public virtual string ExportPath => string.Empty;

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

        public virtual void Dispose()
        {
            if (PreviewFrameProvider is IDisposable disposableProvider)
            {
                disposableProvider.Dispose();
            }
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
