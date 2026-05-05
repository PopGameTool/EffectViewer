using EffectViewer.Settings;
using EffectViewer.Tests.TestUtilities;

namespace EffectViewer.Tests.Settings;

public sealed class UserSettingsStoreTests
{
    [Fact]
    public void SaveAndLoadRoundTripsLanguageAndLayout()
    {
        using TempDirectory temp = new();
        UserSettingsStore store = new(temp.GetPath("settings.json"));
        UserSettings settings = new()
        {
            Language = new LanguageUserSettings
            {
                LanguageCode = "zh-CN",
                CustomLanguageJson = """
                    {"strings":{"Common":{"Save":"Saved!"}}}
                    """
            },
            Layout = new WorkspaceLayoutSettings
            {
                ProjectExplorerDockSide = "Right",
                IsProjectExplorerVisible = false,
                UseMobileLayoutOnWideScreens = true,
                ProjectExplorerLeftWidth = 312d,
                ProjectExplorerRightWidth = 344d,
                EditorLayouts =
                {
                    ["Image"] = new EditorLayoutSettings
                    {
                        IsSidePanelOnLeft = true,
                        IsSidePanelVisible = false,
                        SidePanelWidth = 410d,
                        CompactSidePanelHeight = 220d
                    }
                }
            }
        };

        store.Save(settings);
        UserSettings loaded = store.Load();

        Assert.Equal("zh-CN", loaded.Language.LanguageCode);
        Assert.Contains("Saved!", loaded.Language.CustomLanguageJson);
        Assert.Equal("Right", loaded.Layout.ProjectExplorerDockSide);
        Assert.False(loaded.Layout.IsProjectExplorerVisible);
        Assert.True(loaded.Layout.UseMobileLayoutOnWideScreens);
        Assert.Equal(312d, loaded.Layout.ProjectExplorerLeftWidth);
        Assert.Equal(344d, loaded.Layout.ProjectExplorerRightWidth);

        EditorLayoutSettings imageLayout = loaded.Layout.EditorLayouts["Image"];
        Assert.True(imageLayout.IsSidePanelOnLeft);
        Assert.False(imageLayout.IsSidePanelVisible);
        Assert.Equal(410d, imageLayout.SidePanelWidth);
        Assert.Equal(220d, imageLayout.CompactSidePanelHeight);
    }

    [Fact]
    public void LoadUsesDefaultsWhenSettingsFileIsInvalid()
    {
        using TempDirectory temp = new();
        string settingsPath = temp.GetPath("settings.json");
        File.WriteAllText(settingsPath, "{ not valid json");
        UserSettingsStore store = new(settingsPath);

        UserSettings loaded = store.Load();

        Assert.Equal("Left", loaded.Layout.ProjectExplorerDockSide);
        Assert.True(loaded.Layout.IsProjectExplorerVisible);
        Assert.False(loaded.Layout.UseMobileLayoutOnWideScreens);
        Assert.Equal(280d, loaded.Layout.ProjectExplorerLeftWidth);
        Assert.Equal(280d, loaded.Layout.ProjectExplorerRightWidth);
    }
}
