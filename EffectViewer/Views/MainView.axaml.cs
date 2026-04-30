using Avalonia.Controls;
using Avalonia.Platform.Storage;
using EffectViewer.ViewModels;
using System.Threading.Tasks;

namespace EffectViewer.Views
{
    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
        }

        private async void ImportFolderButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            string path = await PickFolderAsync("Import PopCap resource folder");
            if (!string.IsNullOrWhiteSpace(path) && DataContext is MainViewModel viewModel)
            {
                await viewModel.ImportResourceFolderAsync(path);
            }
        }

        private async void OpenProjectButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            string path = await PickFolderAsync("Open EffectViewer project folder");
            if (!string.IsNullOrWhiteSpace(path) && DataContext is MainViewModel viewModel)
            {
                await viewModel.OpenProjectAsync(path);
            }
        }

        private async Task<string> PickFolderAsync(string title)
        {
            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null)
            {
                return null;
            }

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new()
            {
                Title = title,
                AllowMultiple = false
            });

            return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        }
    }
}
