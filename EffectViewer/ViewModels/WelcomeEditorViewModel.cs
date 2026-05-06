using EffectViewer.Localization;
using EffectViewer.Projects;
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace EffectViewer.ViewModels
{
    public sealed class WelcomeEditorViewModel : EditorViewModelBase
    {
        public string Description => LocalizationManager.Instance.Text("Welcome.Description");
        public string VersionText => LocalizationManager.Instance.Format("About.Version", AppVersion.Current);
        public string CreatePrompt => LocalizationManager.Instance.Text("Welcome.CreatePrompt");
        public string AuthorTitle => LocalizationManager.Instance.Text("Welcome.AuthorTitle");
        public IReadOnlyList<string> AuthorNames =>
        [
            LocalizationManager.Instance.Text("Welcome.AuthorYingFengTingYu"),
            LocalizationManager.Instance.Text("Welcome.AuthorGpt")
        ];
        public string FeaturesTitle => LocalizationManager.Instance.Text("Welcome.FeaturesTitle");
        public IReadOnlyList<string> FeatureDescriptions =>
        [
            LocalizationManager.Instance.Text("Welcome.FeatureProjectIO"),
            LocalizationManager.Instance.Text("Welcome.FeatureImages"),
            LocalizationManager.Instance.Text("Welcome.FeatureReanim"),
            LocalizationManager.Instance.Text("Welcome.FeatureParticles"),
            LocalizationManager.Instance.Text("Welcome.FeatureTrails"),
            LocalizationManager.Instance.Text("Welcome.FeatureShowcases")
        ];

        public ICommand CreateProjectCommand { get; }
        public ICommand OpenProjectCommand { get; }
        public ICommand ImportResourceFolderCommand { get; }
        public ICommand ImportResourcePakCommand { get; }
        public override bool CanClose => false;

        public WelcomeEditorViewModel(
            ICommand createProjectCommand,
            ICommand openProjectCommand,
            ICommand importResourceFolderCommand,
            ICommand importResourcePakCommand)
            : base(LocalizationManager.Instance.Text("Welcome.Title"), EffectAssetKind.Project)
        {
            CreateProjectCommand = createProjectCommand;
            OpenProjectCommand = openProjectCommand;
            ImportResourceFolderCommand = importResourceFolderCommand;
            ImportResourcePakCommand = importResourcePakCommand;
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
            OnPropertyChanged(nameof(VersionText));
            OnPropertyChanged(nameof(CreatePrompt));
            OnPropertyChanged(nameof(AuthorTitle));
            OnPropertyChanged(nameof(AuthorNames));
            OnPropertyChanged(nameof(FeaturesTitle));
            OnPropertyChanged(nameof(FeatureDescriptions));
        }
    }
}
