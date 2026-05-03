using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using EffectViewer.Localization;
using EffectViewer.Projects;
using EffectViewer.ViewModels;
using EffectViewer.Views;
using System.Linq;

namespace EffectViewer
{
    public partial class App : Application
    {
        public static IProjectStorageProvider ProjectStorageProvider { get; set; } = new DefaultProjectStorageProvider();

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            LocalizationManager.Instance.Initialize();
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainViewModel(ProjectStorageProvider)
                };
            }
            else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
            {
                singleViewFactoryApplicationLifetime.MainViewFactory = () => new MainView { DataContext = new MainViewModel(ProjectStorageProvider) };
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                singleViewPlatform.MainView = new MainView
                {
                    DataContext = new MainViewModel(ProjectStorageProvider)
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
