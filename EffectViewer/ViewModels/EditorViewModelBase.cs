using System;
using EffectViewer.Projects;
using EffectViewer.Rendering;
using EffectViewer.Rendering.TextureUpload;

namespace EffectViewer.ViewModels
{
    public abstract partial class EditorViewModelBase : ViewModelBase, IDisposable
    {
        public string Title { get; }
        public EffectAssetKind Kind { get; }
        public RenderFrame PreviewFrame { get; protected set; }
        public IRenderFrameProvider PreviewFrameProvider { get; protected set; }
        public ITextureSource TextureSource { get; protected set; }

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
