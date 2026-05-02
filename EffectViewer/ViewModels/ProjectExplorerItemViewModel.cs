using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EffectViewer.Projects;

namespace EffectViewer.ViewModels
{
    public sealed partial class ProjectExplorerItemViewModel : ViewModelBase
    {
        private string _title;
        private string _path;
        private string _assetId;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value ?? string.Empty);
        }

        public string Path
        {
            get => _path;
            set => SetProperty(ref _path, value ?? string.Empty);
        }

        public string AssetId
        {
            get => _assetId;
            set => SetProperty(ref _assetId, value ?? string.Empty);
        }

        public EffectAssetKind Kind { get; }
        public ObservableCollection<ProjectExplorerItemViewModel> Children { get; } = [];

        [ObservableProperty]
        private bool _isExpanded;

        public bool IsSelectable => Kind is not EffectAssetKind.Folder and not EffectAssetKind.Project;

        public ProjectExplorerItemViewModel(string title, EffectAssetKind kind, string assetId = "", string path = "")
        {
            Title = title;
            Kind = kind;
            AssetId = assetId;
            Path = path;
        }
    }
}
