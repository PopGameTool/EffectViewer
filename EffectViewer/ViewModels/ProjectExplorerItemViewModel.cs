using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EffectViewer.Projects;

namespace EffectViewer.ViewModels
{
    public sealed partial class ProjectExplorerItemViewModel : ViewModelBase
    {
        public string Title { get; }
        public string Path { get; }
        public string AssetId { get; }
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
