using EffectViewer.Localization;
using EffectViewer.Projects;
using System;

namespace EffectViewer.ViewModels
{
    public sealed class WelcomeEditorViewModel : EditorViewModelBase
    {
        public string Description => LocalizationManager.Instance.Text("Welcome.Description");

        public WelcomeEditorViewModel()
            : base(LocalizationManager.Instance.Text("Welcome.Title"), EffectAssetKind.Project)
        {
            LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
        }

        public override void Dispose()
        {
            LocalizationManager.Instance.LanguageChanged -= OnLanguageChanged;
            base.Dispose();
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            Title = LocalizationManager.Instance.Text("Welcome.Title");
            OnPropertyChanged(nameof(Description));
        }
    }
}
