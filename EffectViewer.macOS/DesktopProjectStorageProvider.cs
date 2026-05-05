using System;
using System.IO;
using EffectViewer.Projects;

namespace EffectViewer.macOS
{
    internal sealed class DesktopProjectStorageProvider : IProjectStorageProvider
    {
        public string ProjectsRootPath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EffectViewer",
            "Projects");
    }
}
