using System;
using System.Threading.Tasks;
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

        public string Title { get; }
        public string TabTitle => IsDirty ? $"{Title}*" : Title;
        public string DocumentId { get; }
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
            Title = title;
            Kind = kind;
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
    }
}
