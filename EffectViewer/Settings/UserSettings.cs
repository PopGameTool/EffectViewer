using System.Collections.Generic;

namespace EffectViewer.Settings
{
    public sealed class UserSettings
    {
        public int Version { get; set; } = 1;
        public LanguageUserSettings Language { get; set; } = new();
        public WorkspaceLayoutSettings Layout { get; set; } = new();
    }

    public sealed class LanguageUserSettings
    {
        public string LanguageCode { get; set; } = string.Empty;
        public string CustomLanguageJson { get; set; } = string.Empty;
    }

    public sealed class WorkspaceLayoutSettings
    {
        public string ProjectExplorerDockSide { get; set; } = "Left";
        public bool IsProjectExplorerVisible { get; set; } = true;
        public double ProjectExplorerLeftWidth { get; set; } = 280d;
        public double ProjectExplorerRightWidth { get; set; } = 280d;
        public Dictionary<string, EditorLayoutSettings> EditorLayouts { get; set; } = new();
    }

    public sealed class EditorLayoutSettings
    {
        public bool IsSidePanelOnLeft { get; set; }
        public bool IsSidePanelVisible { get; set; } = true;
        public double SidePanelWidth { get; set; }
        public double CompactSidePanelHeight { get; set; }
    }
}
