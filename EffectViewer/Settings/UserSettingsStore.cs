using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EffectViewer.Settings
{
    public sealed class UserSettingsStore
    {
        private const string SettingsFileName = "settings.json";
        private const double DefaultProjectExplorerWidth = 280d;

        public UserSettingsStore(string settingsFilePath)
        {
            SettingsFilePath = string.IsNullOrWhiteSpace(settingsFilePath)
                ? Path.Combine(CreateDefaultSettingsRootPath(), SettingsFileName)
                : settingsFilePath;
        }

        public string SettingsFilePath { get; }

        public static UserSettingsStore FromProjectsRootPath(string projectsRootPath)
        {
            return new UserSettingsStore(Path.Combine(CreateSettingsRootPath(projectsRootPath), SettingsFileName));
        }

        public UserSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    return Normalize(new UserSettings());
                }

                string json = File.ReadAllText(SettingsFilePath);
                UserSettings settings = JsonSerializer.Deserialize(
                    json,
                    UserSettingsJsonSerializerContext.Default.UserSettings);
                return Normalize(settings);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
            {
                return Normalize(new UserSettings());
            }
        }

        public void Save(UserSettings settings)
        {
            settings = Normalize(settings);
            string directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(
                settings,
                UserSettingsJsonSerializerContext.Default.UserSettings);
            File.WriteAllText(SettingsFilePath, json);
        }

        private static UserSettings Normalize(UserSettings settings)
        {
            settings ??= new UserSettings();
            settings.Language ??= new LanguageUserSettings();
            settings.Layout ??= new WorkspaceLayoutSettings();

            if (string.IsNullOrWhiteSpace(settings.Layout.ProjectExplorerDockSide))
            {
                settings.Layout.ProjectExplorerDockSide = "Left";
            }

            settings.Layout.ProjectExplorerLeftWidth = NormalizePositive(
                settings.Layout.ProjectExplorerLeftWidth,
                DefaultProjectExplorerWidth);
            settings.Layout.ProjectExplorerRightWidth = NormalizePositive(
                settings.Layout.ProjectExplorerRightWidth,
                DefaultProjectExplorerWidth);

            Dictionary<string, EditorLayoutSettings> editorLayouts = new(StringComparer.OrdinalIgnoreCase);
            if (settings.Layout.EditorLayouts is not null)
            {
                foreach ((string key, EditorLayoutSettings value) in settings.Layout.EditorLayouts)
                {
                    if (string.IsNullOrWhiteSpace(key) || value is null)
                    {
                        continue;
                    }

                    editorLayouts[key] = new EditorLayoutSettings
                    {
                        IsSidePanelOnLeft = value.IsSidePanelOnLeft,
                        IsSidePanelVisible = value.IsSidePanelVisible,
                        SidePanelWidth = Math.Max(0d, value.SidePanelWidth),
                        CompactSidePanelHeight = Math.Max(0d, value.CompactSidePanelHeight)
                    };
                }
            }

            settings.Layout.EditorLayouts = editorLayouts;
            return settings;
        }

        private static double NormalizePositive(double value, double fallback)
        {
            return double.IsFinite(value) && value > 0d ? value : fallback;
        }

        private static string CreateSettingsRootPath(string projectsRootPath)
        {
            if (!string.IsNullOrWhiteSpace(projectsRootPath))
            {
                string normalized = projectsRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string parent = Path.GetDirectoryName(normalized);
                if (!string.IsNullOrWhiteSpace(parent))
                {
                    return parent;
                }
            }

            return CreateDefaultSettingsRootPath();
        }

        private static string CreateDefaultSettingsRootPath()
        {
            string basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(basePath))
            {
                basePath = Environment.CurrentDirectory;
            }

            return Path.Combine(basePath, "EffectViewer");
        }
    }
}
