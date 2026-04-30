using EffectViewer.Projects;

namespace EffectViewer.ViewModels
{
    public sealed class WelcomeEditorViewModel : EditorViewModelBase
    {
        public string Description { get; } =
            "Create or open an effect project, then edit images, reanim files, particles, trails, and Lua showcases from the project explorer.";

        public WelcomeEditorViewModel()
            : base("Effect Project", EffectAssetKind.Project)
        {
        }
    }
}
