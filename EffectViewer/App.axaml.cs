using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using EffectViewer.Localization;
using EffectViewer.Projects;
using EffectViewer.Settings;
using EffectViewer.ViewModels;
using EffectViewer.Views;
using System;
using System.Linq;

namespace EffectViewer
{
    public partial class App : Application
    {
        private NativeMenuItem _aboutMenuItem;
        private NativeMenuItem _showMainWindowMenuItem;

        public static IProjectStorageProvider ProjectStorageProvider { get; set; } = new DefaultProjectStorageProvider();
        public static UserSettingsStore SettingsStore { get; private set; }
        public static UserSettings UserSettings { get; private set; } = new();

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            ConfigureSettings();
            LocalizationManager.Instance.Initialize(
                UserSettings.Language.LanguageCode,
                UserSettings.Language.CustomLanguageJson);
            ConfigureNativeMenus();
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = CreateMainViewModel()
                };
            }
            else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
            {
                singleViewFactoryApplicationLifetime.MainViewFactory = () => new MainView { DataContext = CreateMainViewModel() };
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                singleViewPlatform.MainView = new MainView
                {
                    DataContext = CreateMainViewModel()
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static void ConfigureSettings()
        {
            SettingsStore = UserSettingsStore.FromProjectsRootPath(ProjectStorageProvider?.ProjectsRootPath);
            UserSettings = SettingsStore.Load();
        }

        private static MainViewModel CreateMainViewModel()
        {
            return new MainViewModel(ProjectStorageProvider, SettingsStore, UserSettings);
        }

        private void ConfigureNativeMenus()
        {
            if (!OperatingSystem.IsMacOS())
            {
                return;
            }

            _aboutMenuItem = new NativeMenuItem();
            _aboutMenuItem.Click += About_OnClick;
            NativeMenu.SetMenu(this, new NativeMenu
            {
                Items = { _aboutMenuItem }
            });

            _showMainWindowMenuItem = new NativeMenuItem();
            _showMainWindowMenuItem.Click += ShowMainWindow_OnClick;
            NativeDock.SetMenu(this, new NativeMenu
            {
                Items = { _showMainWindowMenuItem }
            });

            LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
            UpdateNativeMenuText();
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            UpdateNativeMenuText();
        }

        private void UpdateNativeMenuText()
        {
            if (_aboutMenuItem is not null)
            {
                _aboutMenuItem.Header = Loc.Text("About.MenuItem");
            }

            if (_showMainWindowMenuItem is not null)
            {
                _showMainWindowMenuItem.Header = Loc.Text("About.ShowMainWindow");
            }
        }

        private void About_OnClick(object sender, EventArgs e)
        {
            if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                return;
            }

            Window aboutWindow = new()
            {
                Title = Loc.Text("About.Title"),
                Width = 360,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = CreateAboutContent(AppVersion.Current)
            };

            if (desktop.MainWindow is { } mainWindow)
            {
                aboutWindow.ShowDialog(mainWindow);
            }
            else
            {
                aboutWindow.Show();
            }
        }

        private void ShowMainWindow_OnClick(object sender, EventArgs e)
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
            {
                mainWindow.Show();
                mainWindow.Activate();
            }
        }

        private static Control CreateAboutContent(string version)
        {
            Button closeButton = new()
            {
                Content = Loc.Text("Common.Ok"),
                HorizontalAlignment = HorizontalAlignment.Right,
                MinWidth = 84
            };

            StackPanel panel = new()
            {
                Spacing = 12,
                Margin = new Thickness(24),
                Children =
                {
                    new TextBlock
                    {
                        Text = "EffectViewer",
                        FontSize = 22,
                        FontWeight = Avalonia.Media.FontWeight.SemiBold
                    },
                    new TextBlock
                    {
                        Text = Loc.Format("About.Version", version),
                        Opacity = 0.75
                    },
                    new TextBlock
                    {
                        Text = Loc.Format(
                            "About.Author",
                            Loc.Text("Welcome.AuthorYingFengTingYu"),
                            Loc.Text("Welcome.AuthorGpt")),
                        Opacity = 0.75
                    },
                    new TextBlock
                    {
                        Text = Loc.Text("About.Description"),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    closeButton
                }
            };

            closeButton.Click += (_, _) =>
            {
                if (TopLevel.GetTopLevel(panel) is Window window)
                {
                    window.Close();
                }
            };

            return panel;
        }

        private static LocalizationManager Loc => LocalizationManager.Instance;
    }
}
