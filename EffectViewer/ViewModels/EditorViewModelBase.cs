using System;
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

        public string Title { get; }
        public EffectAssetKind Kind { get; }

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
            PreviewFrame = EffectPreviewFrameBuilder.BuildPlaceholder(kind, title);
            TextureSource = new GeneratedTextureSource();
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
