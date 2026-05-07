using System;
using System.IO;
using EffectViewer.Projects;

namespace EffectViewer.Windows
{
    internal sealed class WindowsProjectStorageProvider : IProjectStorageProvider
    {
        public string ProjectsRootPath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EffectViewer",
            "Projects");
    }
}
