using Avalonia.Controls;
using EffectViewer.Localization;
using EffectViewer.ViewModels;

namespace EffectViewer.Views
{
    public partial class MainWindow : Window
    {
        private bool _closeConfirmed;

        public MainWindow()
        {
            InitializeComponent();
        }

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            if (_closeConfirmed)
            {
                base.OnClosing(e);
                return;
            }

            if (DataContext is not MainViewModel viewModel)
            {
                base.OnClosing(e);
                return;
            }

            e.Cancel = true;
            if (await viewModel.ConfirmAllUnsavedChangesAsync())
            {
                _closeConfirmed = true;
                Close();
            }
            else
            {
                viewModel.StatusText = LocalizationManager.Instance.Text("Status.CanceledClosingApplication");
            }
        }
    }
}
